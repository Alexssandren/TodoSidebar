namespace TodoSidebar.Janela;

enum Fase
{
    Recolhida,
    AguardandoAbrir,
    Aberta,
    AguardandoRecolher
}

enum ResultadoEsc
{
    Ignorar,
    CancelarEdicao,
    DispensarConfirmacao,
    Recolher
}

static class FaseSidebar
{
    public static Fase AoEntrar(Fase fase) => fase switch
    {
        Fase.Recolhida => Fase.AguardandoAbrir,
        Fase.AguardandoRecolher => Fase.Aberta,
        _ => fase
    };

    public static Fase AoSair(Fase fase, bool botaoPressionado) => fase switch
    {
        Fase.AguardandoAbrir => Fase.Recolhida,
        Fase.Aberta when !botaoPressionado => Fase.AguardandoRecolher,
        _ => fase
    };

    public static Fase AoEsgotarAbertura(Fase fase) =>
        fase == Fase.AguardandoAbrir ? Fase.Aberta : fase;

    public static Fase AoEsgotarFechamento(Fase fase) =>
        fase == Fase.AguardandoRecolher ? Fase.Recolhida : fase;

    public static Fase AoAtalho(Fase fase) =>
        fase is Fase.Aberta or Fase.AguardandoRecolher ? Fase.Recolhida : Fase.Aberta;

    public static ResultadoEsc AoEsc(bool emPrimeiroPlano, bool editando, bool confirmando)
    {
        if (!emPrimeiroPlano)
            return ResultadoEsc.Ignorar;
        if (editando)
            return ResultadoEsc.CancelarEdicao;
        if (confirmando)
            return ResultadoEsc.DispensarConfirmacao;
        return ResultadoEsc.Recolher;
    }

    public static void SelfCheck()
    {
        Exigir(AoEntrar(Fase.Recolhida) == Fase.AguardandoAbrir);
        Exigir(AoSair(Fase.AguardandoAbrir, false) == Fase.Recolhida);
        Exigir(AoEsgotarAbertura(Fase.AguardandoAbrir) == Fase.Aberta);
        Exigir(AoEsgotarAbertura(Fase.Recolhida) == Fase.Recolhida);
        Exigir(AoSair(Fase.Aberta, false) == Fase.AguardandoRecolher);
        Exigir(AoSair(Fase.Aberta, true) == Fase.Aberta);
        Exigir(AoEntrar(Fase.AguardandoRecolher) == Fase.Aberta);
        Exigir(AoEsgotarFechamento(Fase.AguardandoRecolher) == Fase.Recolhida);
        Exigir(AoEsgotarFechamento(Fase.Aberta) == Fase.Aberta);
        Exigir(AoAtalho(Fase.Recolhida) == Fase.Aberta);
        Exigir(AoAtalho(Fase.AguardandoAbrir) == Fase.Aberta);
        Exigir(AoAtalho(Fase.Aberta) == Fase.Recolhida);
        Exigir(AoAtalho(Fase.AguardandoRecolher) == Fase.Recolhida);
        Exigir(AoEsc(false, true, true) == ResultadoEsc.Ignorar);
        Exigir(AoEsc(true, true, true) == ResultadoEsc.CancelarEdicao);
        Exigir(AoEsc(true, false, true) == ResultadoEsc.DispensarConfirmacao);
        Exigir(AoEsc(true, false, false) == ResultadoEsc.Recolher);
    }

    static void Exigir(bool condicao)
    {
        if (!condicao)
            throw new InvalidOperationException("FaseSidebar.SelfCheck falhou.");
    }
}
