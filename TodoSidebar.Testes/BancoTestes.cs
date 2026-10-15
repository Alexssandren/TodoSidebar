using System.IO;
using Microsoft.Data.Sqlite;
using TodoSidebar.Dados;
using Xunit;

namespace TodoSidebar.Testes;

public class BancoTestes : IDisposable
{
    readonly string _pasta = Path.Combine(Path.GetTempPath(), "TodoSidebarTestes", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    [Fact]
    public void PrimeiraAberturaCriaVersao1SemBackup()
    {
        var caminho = Caminho("todo.db");
        using var banco = Abrir(caminho);
        Assert.Equal(1, banco.Versao);
        Assert.False(File.Exists(Caminho("todo.pre-migrate")));
        Assert.False(File.Exists(Caminho("todo.db.bak")));
    }

    [Fact]
    public void NaoRecriaBancoExistente()
    {
        var caminho = Caminho("todo.db");
        using (var banco = Abrir(caminho))
            banco.CriarAvulsa("permanece");

        using var deNovo = Abrir(caminho);
        Assert.Equal("permanece", Assert.Single(deNovo.ListarAvulsas()).Titulo);
        Assert.Equal(1, deNovo.Versao);
    }

    [Fact]
    public void RecusaTituloVazio()
    {
        using var banco = Abrir(Caminho("todo.db"));
        Assert.Throws<ArgumentException>(() => banco.CriarAvulsa("  "));
        Assert.Empty(banco.ListarAvulsas());
    }

    [Fact]
    public void ConcluirEReabrir()
    {
        using var banco = Abrir(Caminho("todo.db"));
        var tarefa = banco.CriarAvulsa("item");
        banco.DefinirConclusao(tarefa.Id, true);
        Assert.True(Assert.Single(banco.ListarAvulsas()).Concluida);
        banco.DefinirConclusao(tarefa.Id, false);
        Assert.False(Assert.Single(banco.ListarAvulsas()).Concluida);
    }

    [Fact]
    public void EditarEExcluirTarefa()
    {
        using var banco = Abrir(Caminho("todo.db"));
        var a = banco.CriarAvulsa("uma");
        banco.CriarAvulsa("duas");
        banco.RenomearTarefa(a.Id, "uma editada");
        Assert.Equal("uma editada", banco.ListarAvulsas()[0].Titulo);
        banco.ExcluirTarefa(a.Id);
        var resto = Assert.Single(banco.ListarAvulsas());
        Assert.Equal("duas", resto.Titulo);
        Assert.Equal(0, resto.Ordem);
    }

    [Fact]
    public void ProjetosExpandirRenomearETarefasProprias()
    {
        using var banco = Abrir(Caminho("todo.db"));
        var projeto = banco.CriarProjeto("Casa");
        banco.CriarNoProjeto(projeto.Id, "varrer");
        banco.DefinirExpandido(projeto.Id, false);
        banco.RenomearProjeto(projeto.Id, "Lar");
        Assert.False(Assert.Single(banco.ListarProjetos()).Expandido);
        Assert.Equal("Lar", banco.ListarProjetos()[0].Nome);
        Assert.Equal("varrer", Assert.Single(banco.ListarDoProjeto(projeto.Id)).Titulo);
        Assert.Empty(banco.ListarAvulsas());
    }

    [Fact]
    public void ExcluirProjetoVazioCompactaOrdem()
    {
        using var banco = Abrir(Caminho("todo.db"));
        var vazio = banco.CriarProjeto("vazio");
        banco.CriarProjeto("fica");
        banco.ExcluirProjeto(vazio.Id);
        Assert.Equal("fica", Assert.Single(banco.ListarProjetos()).Nome);
        Assert.Equal(0, banco.ListarProjetos()[0].Ordem);
    }

    [Fact]
    public void ChaveEstrangeiraImpedeTarefaOrfa()
    {
        using var banco = Abrir(Caminho("todo.db"));
        Assert.Throws<InvalidOperationException>(() => banco.CriarNoProjeto(Guid.NewGuid().ToString("D"), "x"));
    }

    [Fact]
    public void BackupOnlineLeCommitAindaNoWal()
    {
        var origem = Caminho("todo.db");
        using var banco = Abrir(origem);
        banco.CriarAvulsa("no-wal");
        Assert.True(File.Exists(origem + "-wal") || File.Exists(origem));
        var destino = Caminho("copia.db");
        banco.BackupPara(destino);
        using var copia = new Banco(destino);
        copia.Abrir();
        Assert.Equal("no-wal", Assert.Single(copia.ListarAvulsas()).Titulo);
        using var check = new SqliteConnection($"Data Source={destino}");
        check.Open();
        using var cmd = check.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        Assert.Equal("ok", Convert.ToString(cmd.ExecuteScalar())?.ToLowerInvariant());
    }

    [Fact]
    public void VersaoDesconhecidaNaoApagaArquivo()
    {
        var caminho = Caminho("todo.db");
        using (var banco = Abrir(caminho))
            banco.CriarAvulsa("guardar");
        using (var conexao = new SqliteConnection($"Data Source={caminho}"))
        {
            conexao.Open();
            using var cmd = conexao.CreateCommand();
            cmd.CommandText = "PRAGMA user_version = 99";
            cmd.ExecuteNonQuery();
        }

        using var tentativa = new Banco(caminho);
        Assert.Throws<InvalidOperationException>(() => tentativa.Abrir());
        using (var conexao = new SqliteConnection($"Data Source={caminho}"))
        {
            conexao.Open();
            using var cmd = conexao.CreateCommand();
            cmd.CommandText = "PRAGMA user_version";
            Assert.Equal(99, Convert.ToInt32(cmd.ExecuteScalar()));
            cmd.CommandText = "SELECT title FROM tasks";
            Assert.Equal("guardar", Convert.ToString(cmd.ExecuteScalar()));
        }
    }

    Banco Abrir(string caminho)
    {
        var banco = new Banco(caminho);
        banco.Abrir();
        return banco;
    }

    string Caminho(string nome)
    {
        Directory.CreateDirectory(_pasta);
        return Path.Combine(_pasta, nome);
    }
}
