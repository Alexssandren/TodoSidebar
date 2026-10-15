using Microsoft.Data.Sqlite;

namespace TodoSidebar.Dados;

static class Esquema
{
    public const int VersaoAtual = 1;

    public static SqliteConnection AbrirArquivo(string caminho)
    {
        var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        conexao.Open();
        using var pragma = conexao.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL;";
        pragma.ExecuteNonQuery();
        return conexao;
    }

    public static void Garantir(SqliteConnection conexao)
    {
        int versao = LerVersao(conexao);
        if (versao == VersaoAtual)
            return;
        if (versao == 0 && SemTabelas(conexao))
        {
            CriarV1(conexao);
            return;
        }

        throw new InvalidOperationException($"Banco versão {versao} não suportada.");
    }

    public static int LerVersao(SqliteConnection conexao)
    {
        using var cmd = conexao.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    static bool SemTabelas(SqliteConnection conexao)
    {
        using var cmd = conexao.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        return Convert.ToInt32(cmd.ExecuteScalar()) == 0;
    }

    static void CriarV1(SqliteConnection conexao)
    {
        using var tx = conexao.BeginTransaction();
        using (var cmd = conexao.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                CREATE TABLE projects (
                  id TEXT PRIMARY KEY,
                  name TEXT NOT NULL,
                  sort_order INTEGER NOT NULL,
                  expanded INTEGER NOT NULL DEFAULT 1,
                  created_at TEXT NOT NULL,
                  updated_at TEXT NOT NULL
                );
                CREATE TABLE tasks (
                  id TEXT PRIMARY KEY,
                  project_id TEXT NULL REFERENCES projects(id) ON DELETE RESTRICT,
                  title TEXT NOT NULL,
                  completed INTEGER NOT NULL DEFAULT 0 CHECK (completed IN (0, 1)),
                  sort_order INTEGER NOT NULL,
                  created_at TEXT NOT NULL,
                  updated_at TEXT NOT NULL
                );
                CREATE TABLE settings (
                  key TEXT PRIMARY KEY,
                  value TEXT NOT NULL
                );
                CREATE UNIQUE INDEX idx_projects_sort ON projects(sort_order);
                CREATE UNIQUE INDEX idx_tasks_loose_sort ON tasks(sort_order) WHERE project_id IS NULL;
                CREATE UNIQUE INDEX idx_tasks_project_sort ON tasks(project_id, sort_order) WHERE project_id IS NOT NULL;
                PRAGMA user_version = 1;
                """;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
