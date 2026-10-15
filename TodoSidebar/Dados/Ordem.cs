using Microsoft.Data.Sqlite;

namespace TodoSidebar.Dados;

static class Ordem
{
    public static void Aplicar(SqliteConnection conexao, SqliteTransaction tx, string sql, IReadOnlyList<string> ids)
    {
        for (int i = 0; i < ids.Count; i++)
            Escrever(conexao, tx, sql, ids[i], -1 - i);
        for (int i = 0; i < ids.Count; i++)
            Escrever(conexao, tx, sql, ids[i], i);
    }

    public static void ExigirPermutacao(IReadOnlyList<string> atuais, IReadOnlyList<string> novas)
    {
        if (atuais.Count != novas.Count || atuais.Count != atuais.Distinct().Count() || novas.Count != novas.Distinct().Count())
            throw new InvalidOperationException("A ordem informada não é uma permutação do grupo.");
        if (atuais.Count != atuais.Intersect(novas, StringComparer.Ordinal).Count())
            throw new InvalidOperationException("A reordenação não pode misturar grupos.");
    }

    static void Escrever(SqliteConnection conexao, SqliteTransaction tx, string sql, string id, int ordem)
    {
        using var cmd = conexao.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$o", ordem);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
