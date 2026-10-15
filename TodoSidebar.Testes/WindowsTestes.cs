using TodoSidebar.Janela;
using Xunit;

namespace TodoSidebar.Testes;

public class WindowsTestes
{
    [Fact]
    public void InicioDesligadaPorPadrao()
    {
        string? valor = null;
        var inicio = new InicioAutomatico(() => valor, v => valor = v, () => valor = null);
        Assert.Equal(EstadoInicio.Desligada, inicio.Ler(@"C:\App\TodoSidebar.exe"));
    }

    [Fact]
    public void InicioLigaComCaminhoEntreAspas()
    {
        string? valor = null;
        var inicio = new InicioAutomatico(() => valor, v => valor = v, () => valor = null);
        var exe = @"C:\Pasta Com Espaco\TodoSidebar.exe";
        Assert.Equal(EstadoInicio.Ligada, inicio.Ligar(exe));
        Assert.Equal("\"" + exe + "\"", valor);
        Assert.Equal(EstadoInicio.Ligada, inicio.Ler(exe));
    }

    [Fact]
    public void InicioDesligaRemoveValor()
    {
        string? valor = "\"C:\\App\\TodoSidebar.exe\"";
        var inicio = new InicioAutomatico(() => valor, v => valor = v, () => valor = null);
        Assert.Equal(EstadoInicio.Desligada, inicio.Desligar(@"C:\App\TodoSidebar.exe"));
        Assert.Null(valor);
    }

    [Fact]
    public void InicioDetectaOutroExecutavel()
    {
        string? valor = "\"C:\\Outro\\TodoSidebar.exe\"";
        var inicio = new InicioAutomatico(() => valor, v => valor = v, () => valor = null);
        Assert.Equal(EstadoInicio.OutroExecutavel, inicio.Ler(@"C:\App\TodoSidebar.exe"));
        Assert.Equal(EstadoInicio.Ligada, inicio.Ligar(@"C:\App\TodoSidebar.exe"));
        Assert.Equal("\"C:\\App\\TodoSidebar.exe\"", valor);
    }

    [Fact]
    public void InicioFalhaNaoMenteSucesso()
    {
        var inicio = new InicioAutomatico(
            () => throw new UnauthorizedAccessException(),
            _ => throw new UnauthorizedAccessException(),
            () => throw new UnauthorizedAccessException());
        Assert.Equal(EstadoInicio.Indisponivel, inicio.Ler(@"C:\App\TodoSidebar.exe"));
        Assert.Equal(EstadoInicio.Indisponivel, inicio.Ligar(@"C:\App\TodoSidebar.exe"));
        Assert.Equal(EstadoInicio.Indisponivel, inicio.Desligar(@"C:\App\TodoSidebar.exe"));
    }

    [Fact]
    public void PosicaoRespeitaAreaUtilEDpi()
    {
        var area = new Retangulo(0, 0, 1920, 1040);
        var alca = Posicao.Alca(area, 96);
        var painel = Posicao.Painel(area, 96);
        Assert.Equal(area.Direita, alca.Direita);
        Assert.Equal(area.Direita, painel.Direita);
        Assert.Equal(18, alca.Largura);
        Assert.Equal(360, painel.Largura);
        Assert.Equal(area.Y, painel.Y);
        Assert.Equal(area.Altura, painel.Altura);

        var barraDireita = new Retangulo(0, 0, 1800, 1080);
        Assert.Equal(1800, Posicao.Alca(barraDireita, 96).Direita);
        Assert.Equal(1800, Posicao.Painel(barraDireita, 96).Direita);

        var alca150 = Posicao.Alca(area, 144);
        Assert.Equal(27, alca150.Largura);
        Assert.Equal(area.Direita, alca150.Direita);
        Assert.True(alca150.Y >= area.Y);
        Assert.True(alca150.Baixo <= area.Baixo);

        var estreita = new Retangulo(10, 20, 80, 50);
        var painelEstreito = Posicao.Painel(estreita, 96);
        Assert.Equal(80, painelEstreito.Largura);
        Assert.Equal(estreita.Direita, painelEstreito.Direita);
        Assert.Equal(50, Posicao.Alca(estreita, 96).Altura);
    }

    [Fact]
    public void AtalhoNaoRegistraDuasVezes()
    {
        int vezes = 0;
        var atalho = new AtalhoUnico();
        Assert.True(atalho.Registrar(() => { vezes++; return true; }));
        Assert.True(atalho.Registrar(() => { vezes++; return true; }));
        Assert.Equal(1, vezes);
        Assert.True(atalho.Ligado);
        int libertacoes = 0;
        atalho.Liberar(() => libertacoes++);
        atalho.Liberar(() => libertacoes++);
        Assert.Equal(1, libertacoes);
        Assert.False(atalho.Ligado);
        Assert.True(atalho.Registrar(() => { vezes++; return true; }));
        Assert.Equal(2, vezes);
    }
}
