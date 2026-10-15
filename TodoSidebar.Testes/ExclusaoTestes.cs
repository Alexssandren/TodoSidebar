using System.IO;
using TodoSidebar.Dados;
using Xunit;

namespace TodoSidebar.Testes;

public class ExclusaoTestes : IDisposable
{
    readonly string _pasta = Path.Combine(Path.GetTempPath(), "TodoSidebarExclusao", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    [Fact]
    public void ProjetoVazioSomeEResumoZerado()
    {
        using var banco = Abrir();
        var projeto = banco.CriarProjeto("Vista-se");
        var resumo = banco.ResumoExclusao(projeto.Id);
        Assert.Equal("Vista-se", resumo.Nome);
        Assert.Equal(0, resumo.Abertas);
        Assert.Equal(0, resumo.Concluidas);
        banco.ExcluirProjeto(projeto.Id);
        Assert.Empty(banco.ListarProjetos());
        Assert.Empty(banco.ListarAvulsas());
    }

    [Fact]
    public void SomenteAbertasViramAvulsasNoFimPreservandoDados()
    {
        using var banco = Abrir();
        var existente = banco.CriarAvulsa("já avulsa");
        var projeto = banco.CriarProjeto("Obra");
        var a = banco.CriarNoProjeto(projeto.Id, "abrir");
        var b = banco.CriarNoProjeto(projeto.Id, "fechar");
        var criado = banco.ListarDoProjeto(projeto.Id).First(t => t.Id == a.Id).CriadaEm;
        banco.ExcluirProjeto(projeto.Id);
        Assert.Empty(banco.ListarProjetos());
        var avulsas = banco.ListarAvulsas();
        Assert.Equal(["já avulsa", "abrir", "fechar"], avulsas.Select(t => t.Titulo).ToArray());
        Assert.Equal([0, 1, 2], avulsas.Select(t => t.Ordem).ToArray());
        Assert.Equal(a.Id, avulsas[1].Id);
        Assert.Null(avulsas[1].ProjetoId);
        Assert.False(avulsas[1].Concluida);
        Assert.Equal(criado, avulsas[1].CriadaEm);
        Assert.Equal(existente.Id, avulsas[0].Id);
    }

    [Fact]
    public void SomenteConcluidasSaoApagadas()
    {
        using var banco = Abrir();
        var projeto = banco.CriarProjeto("Limpeza");
        var feita = banco.CriarNoProjeto(projeto.Id, "feita");
        banco.DefinirConclusao(feita.Id, true);
        banco.ExcluirProjeto(projeto.Id);
        Assert.Empty(banco.ListarProjetos());
        Assert.Empty(banco.ListarAvulsas());
    }

    [Fact]
    public void MistoPreservaAbertasExcluiConcluidasENaoMexeEmOutros()
    {
        using var banco = Abrir();
        banco.CriarAvulsa("outra avulsa");
        var outro = banco.CriarProjeto("Outro");
        banco.CriarNoProjeto(outro.Id, "de outro");
        var projeto = banco.CriarProjeto("Vista-se");
        var aberta = banco.CriarNoProjeto(projeto.Id, "pendente");
        var feita = banco.CriarNoProjeto(projeto.Id, "pronta");
        banco.DefinirConclusao(feita.Id, true);
        var resumo = banco.ResumoExclusao(projeto.Id);
        Assert.Equal(1, resumo.Abertas);
        Assert.Equal(1, resumo.Concluidas);
        banco.ExcluirProjeto(projeto.Id);
        Assert.Equal(["Outro"], banco.ListarProjetos().Select(p => p.Nome).ToArray());
        Assert.Equal(0, banco.ListarProjetos()[0].Ordem);
        Assert.Equal(["outra avulsa", "pendente"], banco.ListarAvulsas().Select(t => t.Titulo).ToArray());
        Assert.Equal([0, 1], banco.ListarAvulsas().Select(t => t.Ordem).ToArray());
        Assert.Equal(aberta.Id, banco.ListarAvulsas()[1].Id);
        Assert.Single(banco.ListarDoProjeto(outro.Id));
    }

    [Fact]
    public void CancelamentoNaoChamaExclusao()
    {
        using var banco = Abrir();
        var projeto = banco.CriarProjeto("fica");
        banco.CriarNoProjeto(projeto.Id, "item");
        var resumo = banco.ResumoExclusao(projeto.Id);
        Assert.Equal(1, resumo.Abertas);
        Assert.Equal("fica", Assert.Single(banco.ListarProjetos()).Nome);
        Assert.Single(banco.ListarDoProjeto(projeto.Id));
    }

    [Fact]
    public void FalhaAntesDoCommitNaoAlteraNada()
    {
        using var banco = Abrir();
        var projeto = banco.CriarProjeto("Vista-se");
        var aberta = banco.CriarNoProjeto(projeto.Id, "aberta");
        var feita = banco.CriarNoProjeto(projeto.Id, "feita");
        banco.DefinirConclusao(feita.Id, true);
        Assert.Throws<InvalidOperationException>(() =>
            banco.ExcluirProjeto(projeto.Id, () => throw new InvalidOperationException("falha")));
        Assert.Equal("Vista-se", Assert.Single(banco.ListarProjetos()).Nome);
        Assert.Equal(["aberta", "feita"], banco.ListarDoProjeto(projeto.Id).Select(t => t.Titulo).ToArray());
        Assert.Empty(banco.ListarAvulsas());
        Assert.Equal(aberta.Id, banco.ListarDoProjeto(projeto.Id)[0].Id);
        Assert.True(banco.ListarDoProjeto(projeto.Id)[1].Concluida);
    }

    Banco Abrir()
    {
        Directory.CreateDirectory(_pasta);
        var banco = new Banco(Path.Combine(_pasta, Guid.NewGuid().ToString("N") + ".db"));
        banco.Abrir();
        return banco;
    }
}
