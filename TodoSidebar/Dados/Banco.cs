using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;
using TodoSidebar.Registros;

namespace TodoSidebar.Dados;

public sealed class Banco : IDisposable
{
    readonly string _caminho;
    SqliteConnection? _conexao;

    public Banco(string caminho) => _caminho = caminho;

    public void Abrir()
    {
        var pasta = Path.GetDirectoryName(_caminho);
        if (!string.IsNullOrEmpty(pasta))
            Directory.CreateDirectory(pasta);

        _conexao = Esquema.AbrirArquivo(_caminho);
        Esquema.Garantir(_conexao);
    }

    public void Dispose()
    {
        _conexao?.Dispose();
        _conexao = null;
    }

    public int Versao => Esquema.LerVersao(Conexao());

    public IReadOnlyList<Tarefa> ListarAvulsas() =>
        ListarTarefas("WHERE project_id IS NULL");

    public IReadOnlyList<Tarefa> ListarDoProjeto(string projetoId) =>
        ListarTarefas("WHERE project_id = $p", projetoId);

    public IReadOnlyList<Projeto> ListarProjetos()
    {
        using var cmd = Conexao().CreateCommand();
        cmd.CommandText = """
            SELECT id, name, sort_order, expanded, created_at, updated_at
            FROM projects
            ORDER BY sort_order
            """;
        using var leitor = cmd.ExecuteReader();
        var lista = new List<Projeto>();
        while (leitor.Read())
            lista.Add(LerProjeto(leitor));
        return lista;
    }

    public Tarefa CriarAvulsa(string titulo) =>
        CriarTarefa(titulo, null);

    public Tarefa CriarNoProjeto(string projetoId, string titulo) =>
        CriarTarefa(titulo, projetoId);

    public void RenomearTarefa(string id, string titulo)
    {
        titulo = ExigirTexto(titulo);
        using var cmd = Conexao().CreateCommand();
        cmd.CommandText = """
            UPDATE tasks SET title = $t, updated_at = $u WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$t", titulo);
        cmd.Parameters.AddWithValue("$u", Agora());
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("Tarefa não encontrada.");
    }

    public void DefinirConclusao(string id, bool concluida)
    {
        using var cmd = Conexao().CreateCommand();
        cmd.CommandText = """
            UPDATE tasks SET completed = $c, updated_at = $u WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$c", concluida ? 1 : 0);
        cmd.Parameters.AddWithValue("$u", Agora());
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("Tarefa não encontrada.");
    }

    public void ExcluirTarefa(string id)
    {
        using var tx = Conexao().BeginTransaction(IsolationLevel.Serializable);
        string? projetoId;
        using (var cmd = Conexao().CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT project_id FROM tasks WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var leitor = cmd.ExecuteReader();
            if (!leitor.Read())
                throw new InvalidOperationException("Tarefa não encontrada.");
            projetoId = leitor.IsDBNull(0) ? null : leitor.GetString(0);
        }

        using (var cmd = Conexao().CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM tasks WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        CompactarTarefas(tx, projetoId);
        tx.Commit();
    }

    public void ReordenarAvulsas(IReadOnlyList<string> ids) =>
        ReordenarTarefas(null, ids);

    public void ReordenarDoProjeto(string projetoId, IReadOnlyList<string> ids) =>
        ReordenarTarefas(projetoId, ids);

    public void ReordenarProjetos(IReadOnlyList<string> ids)
    {
        using var tx = Conexao().BeginTransaction(IsolationLevel.Serializable);
        Ordem.ExigirPermutacao(IdsProjetos(tx), ids);
        Ordem.Aplicar(Conexao(), tx, "UPDATE projects SET sort_order = $o WHERE id = $id", ids);
        tx.Commit();
    }

    public IReadOnlyList<Tarefa> BuscarTarefas(string termo)
    {
        termo = termo.Trim();
        if (termo.Length == 0)
            return [];
        using var cmd = Conexao().CreateCommand();
        cmd.CommandText = """
            SELECT id, project_id, title, completed, sort_order, created_at, updated_at
            FROM tasks
            WHERE instr(lower(title), lower($t)) > 0
            ORDER BY project_id IS NULL DESC, project_id, sort_order
            """;
        cmd.Parameters.AddWithValue("$t", termo);
        using var leitor = cmd.ExecuteReader();
        var lista = new List<Tarefa>();
        while (leitor.Read())
            lista.Add(LerTarefa(leitor));
        return lista;
    }

    public Projeto CriarProjeto(string nome)
    {
        nome = ExigirTexto(nome);
        using var tx = Conexao().BeginTransaction(IsolationLevel.Serializable);
        int ordem = Contar(tx, "SELECT COUNT(*) FROM projects");
        var agora = Agora();
        var projeto = new Projeto(Guid.NewGuid().ToString("D"), nome, ordem, true, agora, agora);
        using (var cmd = Conexao().CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO projects (id, name, sort_order, expanded, created_at, updated_at)
                VALUES ($id, $n, $o, 1, $c, $u)
                """;
            cmd.Parameters.AddWithValue("$id", projeto.Id);
            cmd.Parameters.AddWithValue("$n", nome);
            cmd.Parameters.AddWithValue("$o", ordem);
            cmd.Parameters.AddWithValue("$c", agora);
            cmd.Parameters.AddWithValue("$u", agora);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return projeto;
    }

    public void RenomearProjeto(string id, string nome)
    {
        nome = ExigirTexto(nome);
        using var cmd = Conexao().CreateCommand();
        cmd.CommandText = """
            UPDATE projects SET name = $n, updated_at = $u WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$n", nome);
        cmd.Parameters.AddWithValue("$u", Agora());
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("Projeto não encontrado.");
    }

    public void DefinirExpandido(string id, bool expandido)
    {
        using var cmd = Conexao().CreateCommand();
        cmd.CommandText = """
            UPDATE projects SET expanded = $e, updated_at = $u WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$e", expandido ? 1 : 0);
        cmd.Parameters.AddWithValue("$u", Agora());
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteNonQuery() != 1)
            throw new InvalidOperationException("Projeto não encontrado.");
    }

    public ResumoExclusao ResumoExclusao(string id)
    {
        using var cmd = Conexao().CreateCommand();
        cmd.CommandText = """
            SELECT p.name,
                   (SELECT COUNT(*) FROM tasks WHERE project_id = p.id AND completed = 0),
                   (SELECT COUNT(*) FROM tasks WHERE project_id = p.id AND completed = 1)
            FROM projects p WHERE p.id = $id
            """;
        cmd.Parameters.AddWithValue("$id", id);
        using var leitor = cmd.ExecuteReader();
        if (!leitor.Read())
            throw new InvalidOperationException("Projeto não encontrado.");
        return new ResumoExclusao(leitor.GetString(0), leitor.GetInt32(1), leitor.GetInt32(2));
    }

    public void ExcluirProjeto(string id, Action? falharAntesDoCommit = null)
    {
        using var tx = Conexao().BeginTransaction(IsolationLevel.Serializable);
        var abertas = IdsTarefas(tx, id, soAbertas: true);

        using (var cmd = Conexao().CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT name FROM projects WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            if (cmd.ExecuteScalar() is null)
                throw new InvalidOperationException("Projeto não encontrado.");
        }

        var avulsas = IdsTarefas(tx, null);
        avulsas.AddRange(abertas);

        for (int i = 0; i < abertas.Count; i++)
        {
            using var cmd = Conexao().CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                UPDATE tasks
                SET project_id = NULL, sort_order = $o, updated_at = $u
                WHERE id = $id
                """;
            cmd.Parameters.AddWithValue("$o", -1000 - i);
            cmd.Parameters.AddWithValue("$u", Agora());
            cmd.Parameters.AddWithValue("$id", abertas[i]);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = Conexao().CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM tasks WHERE project_id = $p AND completed = 1";
            cmd.Parameters.AddWithValue("$p", id);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = Conexao().CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM projects WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            if (cmd.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("Não foi possível excluir o projeto. Nenhuma alteração foi salva.");
        }

        Ordem.Aplicar(Conexao(), tx, "UPDATE tasks SET sort_order = $o WHERE id = $id", avulsas);
        CompactarProjetos(tx);
        falharAntesDoCommit?.Invoke();
        tx.Commit();
    }

    public void BackupPara(string destino)
    {
        if (File.Exists(destino))
            throw new InvalidOperationException("O arquivo de backup já existe.");

        using var copia = Esquema.AbrirArquivo(destino);
        Conexao().BackupDatabase(copia);
        using var check = copia.CreateCommand();
        check.CommandText = "PRAGMA integrity_check";
        var resultado = Convert.ToString(check.ExecuteScalar());
        if (!string.Equals(resultado, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup inválido: " + resultado);
        if (Esquema.LerVersao(copia) != Esquema.LerVersao(Conexao()))
            throw new InvalidOperationException("Backup com versão diferente da origem.");
    }

    Tarefa CriarTarefa(string titulo, string? projetoId)
    {
        titulo = ExigirTexto(titulo);
        using var tx = Conexao().BeginTransaction(IsolationLevel.Serializable);
        if (projetoId != null)
            ExigirProjeto(tx, projetoId);

        int ordem = projetoId == null
            ? Contar(tx, "SELECT COUNT(*) FROM tasks WHERE project_id IS NULL")
            : Contar(tx, "SELECT COUNT(*) FROM tasks WHERE project_id = $p", projetoId);
        var agora = Agora();
        var tarefa = new Tarefa(Guid.NewGuid().ToString("D"), projetoId, titulo, false, ordem, agora, agora);
        using (var cmd = Conexao().CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO tasks (id, project_id, title, completed, sort_order, created_at, updated_at)
                VALUES ($id, $p, $t, 0, $o, $c, $u)
                """;
            cmd.Parameters.AddWithValue("$id", tarefa.Id);
            cmd.Parameters.AddWithValue("$p", (object?)projetoId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$t", titulo);
            cmd.Parameters.AddWithValue("$o", ordem);
            cmd.Parameters.AddWithValue("$c", agora);
            cmd.Parameters.AddWithValue("$u", agora);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return tarefa;
    }

    List<Tarefa> ListarTarefas(string filtro, string? projetoId = null)
    {
        using var cmd = Conexao().CreateCommand();
        cmd.CommandText = $"""
            SELECT id, project_id, title, completed, sort_order, created_at, updated_at
            FROM tasks
            {filtro}
            ORDER BY sort_order
            """;
        if (projetoId != null)
            cmd.Parameters.AddWithValue("$p", projetoId);
        using var leitor = cmd.ExecuteReader();
        var lista = new List<Tarefa>();
        while (leitor.Read())
            lista.Add(LerTarefa(leitor));
        return lista;
    }

    Tarefa? LerTarefa(string id)
    {
        using var cmd = Conexao().CreateCommand();
        cmd.CommandText = """
            SELECT id, project_id, title, completed, sort_order, created_at, updated_at
            FROM tasks WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", id);
        using var leitor = cmd.ExecuteReader();
        return leitor.Read() ? LerTarefa(leitor) : null;
    }

    static Tarefa LerTarefa(SqliteDataReader leitor) => new(
        leitor.GetString(0),
        leitor.IsDBNull(1) ? null : leitor.GetString(1),
        leitor.GetString(2),
        leitor.GetInt32(3) == 1,
        leitor.GetInt32(4),
        leitor.GetString(5),
        leitor.GetString(6));

    static Projeto LerProjeto(SqliteDataReader leitor) => new(
        leitor.GetString(0),
        leitor.GetString(1),
        leitor.GetInt32(2),
        leitor.GetInt32(3) == 1,
        leitor.GetString(4),
        leitor.GetString(5));

    void ReordenarTarefas(string? projetoId, IReadOnlyList<string> ids)
    {
        using var tx = Conexao().BeginTransaction(IsolationLevel.Serializable);
        Ordem.ExigirPermutacao(IdsTarefas(tx, projetoId), ids);
        Ordem.Aplicar(Conexao(), tx, "UPDATE tasks SET sort_order = $o WHERE id = $id", ids);
        tx.Commit();
    }

    void CompactarTarefas(SqliteTransaction tx, string? projetoId) =>
        Ordem.Aplicar(Conexao(), tx, "UPDATE tasks SET sort_order = $o WHERE id = $id", IdsTarefas(tx, projetoId));

    void CompactarProjetos(SqliteTransaction tx) =>
        Ordem.Aplicar(Conexao(), tx, "UPDATE projects SET sort_order = $o WHERE id = $id", IdsProjetos(tx));

    List<string> IdsTarefas(SqliteTransaction tx, string? projetoId, bool soAbertas = false)
    {
        using var cmd = Conexao().CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = projetoId == null
            ? "SELECT id FROM tasks WHERE project_id IS NULL ORDER BY sort_order"
            : soAbertas
                ? "SELECT id FROM tasks WHERE project_id = $p AND completed = 0 ORDER BY sort_order"
                : "SELECT id FROM tasks WHERE project_id = $p ORDER BY sort_order";
        if (projetoId != null)
            cmd.Parameters.AddWithValue("$p", projetoId);
        return LerIds(cmd);
    }

    List<string> IdsProjetos(SqliteTransaction tx)
    {
        using var cmd = Conexao().CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM projects ORDER BY sort_order";
        return LerIds(cmd);
    }

    static List<string> LerIds(SqliteCommand cmd)
    {
        using var leitor = cmd.ExecuteReader();
        var ids = new List<string>();
        while (leitor.Read())
            ids.Add(leitor.GetString(0));
        return ids;
    }

    int Contar(SqliteTransaction tx, string sql, string? projetoId = null)
    {
        using var cmd = Conexao().CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        if (projetoId != null)
            cmd.Parameters.AddWithValue("$p", projetoId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    void ExigirProjeto(SqliteTransaction tx, string id)
    {
        using var cmd = Conexao().CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT 1 FROM projects WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteScalar() is null)
            throw new InvalidOperationException("Projeto não encontrado.");
    }

    SqliteConnection Conexao() =>
        _conexao ?? throw new InvalidOperationException("Banco fechado.");

    static string ExigirTexto(string texto)
    {
        texto = texto.Trim();
        if (texto.Length == 0)
            throw new ArgumentException("O título não pode ficar vazio.");
        return texto;
    }

    static string Agora() => DateTime.UtcNow.ToString("o");
}
