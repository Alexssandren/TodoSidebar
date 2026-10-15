using System.IO;
using TodoSidebar.Dados;
using Xunit;

namespace TodoSidebar.Testes;

public class OrdemTestes : IDisposable
{
    readonly string _pasta = Path.Combine(Path.GetTempPath(), "TodoSidebarOrdem", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    [Fact]
    public void BuscaFiltraPorTituloSemAlterarOrdem()
    {
        using var banco = Abrir();
        banco.CriarAvulsa("comprar pão");
        banco.CriarAvulsa("ligar para Ana");
        var projeto = banco.CriarProjeto("Obra");
        banco.CriarNoProjeto(projeto.Id, "comprar cimento");
        var antes = banco.ListarAvulsas().Select(t => t.Ordem).ToArray();
        var achadas = banco.BuscarTarefas("comprar");
        Assert.Equal(2, achadas.Count);
        Assert.All(achadas, t => Assert.Contains("comprar", t.Titulo, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(antes, banco.ListarAvulsas().Select(t => t.Ordem).ToArray());
        Assert.Empty(banco.BuscarTarefas("   "));
    }

    [Fact]
    public void ReordenaAvulsasEPersisteAposReabrir()
    {
        var caminho = Caminho("todo.db");
        string[] ordem;
        using (var banco = Abrir(caminho))
        {
            var a = banco.CriarAvulsa("a");
            var b = banco.CriarAvulsa("b");
            var c = banco.CriarAvulsa("c");
            banco.ReordenarAvulsas([c.Id, a.Id, b.Id]);
            ordem = banco.ListarAvulsas().Select(t => t.Titulo).ToArray();
            Assert.Equal(["c", "a", "b"], ordem);
            Assert.Equal([0, 1, 2], banco.ListarAvulsas().Select(t => t.Ordem).ToArray());
        }

        using var deNovo = Abrir(caminho);
        Assert.Equal(ordem, deNovo.ListarAvulsas().Select(t => t.Titulo).ToArray());
        Assert.Equal([0, 1, 2], deNovo.ListarAvulsas().Select(t => t.Ordem).ToArray());
    }

    [Fact]
    public void ReordenaProjetosETarefasDoProjeto()
    {
        using var banco = Abrir();
        var p1 = banco.CriarProjeto("um");
        var p2 = banco.CriarProjeto("dois");
        var x = banco.CriarNoProjeto(p1.Id, "x");
        var y = banco.CriarNoProjeto(p1.Id, "y");
        banco.ReordenarProjetos([p2.Id, p1.Id]);
        banco.ReordenarDoProjeto(p1.Id, [y.Id, x.Id]);
        Assert.Equal(["dois", "um"], banco.ListarProjetos().Select(p => p.Nome).ToArray());
        Assert.Equal(["y", "x"], banco.ListarDoProjeto(p1.Id).Select(t => t.Titulo).ToArray());
        Assert.Equal([0, 1], banco.ListarProjetos().Select(p => p.Ordem).ToArray());
        Assert.Equal([0, 1], banco.ListarDoProjeto(p1.Id).Select(t => t.Ordem).ToArray());
    }

    [Fact]
    public void RecusaMovimentacaoEntreGruposENaoAlteraDados()
    {
        using var banco = Abrir();
        var avulsa = banco.CriarAvulsa("avulsa");
        banco.CriarAvulsa("outra");
        var projeto = banco.CriarProjeto("p");
        var interna = banco.CriarNoProjeto(projeto.Id, "interna");
        Assert.Throws<InvalidOperationException>(() => banco.ReordenarDoProjeto(projeto.Id, [avulsa.Id]));
        Assert.Throws<InvalidOperationException>(() => banco.ReordenarAvulsas([interna.Id, avulsa.Id]));
        Assert.Throws<InvalidOperationException>(() => banco.ReordenarProjetos([projeto.Id, avulsa.Id]));
        Assert.Equal("avulsa", banco.ListarAvulsas()[0].Titulo);
        Assert.Equal("interna", Assert.Single(banco.ListarDoProjeto(projeto.Id)).Titulo);
        Assert.Equal(0, banco.ListarAvulsas()[0].Ordem);
        Assert.Equal(0, banco.ListarDoProjeto(projeto.Id)[0].Ordem);
    }

    Banco Abrir(string? caminho = null)
    {
        var banco = new Banco(caminho ?? Caminho(Guid.NewGuid().ToString("N") + ".db"));
        banco.Abrir();
        return banco;
    }

    string Caminho(string nome)
    {
        Directory.CreateDirectory(_pasta);
        return Path.Combine(_pasta, nome);
    }
}
