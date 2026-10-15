namespace TodoSidebar.Janela;

readonly record struct Retangulo(int X, int Y, int Largura, int Altura)
{
    public int Direita => X + Largura;
    public int Baixo => Y + Altura;
}

static class Posicao
{
    public const double AlcaLarguraDip = 18;
    public const double AlcaAlturaDip = 96;
    public const double PainelLarguraDip = 360;

    public static Retangulo Alca(Retangulo area, uint dpi)
    {
        int largura = DipParaPx(AlcaLarguraDip, dpi);
        int altura = Math.Min(DipParaPx(AlcaAlturaDip, dpi), area.Altura);
        int x = area.Direita - largura;
        int y = area.Y + (area.Altura - altura) / 2;
        return new Retangulo(x, y, largura, altura);
    }

    public static Retangulo Painel(Retangulo area, uint dpi)
    {
        int largura = Math.Min(DipParaPx(PainelLarguraDip, dpi), area.Largura);
        int x = area.Direita - largura;
        return new Retangulo(x, area.Y, largura, area.Altura);
    }

    public static Retangulo Interpolar(Retangulo de, Retangulo para, double t)
    {
        return new Retangulo(
            Misturar(de.X, para.X, t),
            Misturar(de.Y, para.Y, t),
            Misturar(de.Largura, para.Largura, t),
            Misturar(de.Altura, para.Altura, t));
    }

    public static int DipParaPx(double dip, uint dpi) =>
        (int)Math.Round(dip * dpi / 96.0);

    public static double PxParaDip(int px, uint dpi) =>
        px * 96.0 / dpi;

    public static void SelfCheck()
    {
        var area = new Retangulo(40, 10, 1920, 1000);
        foreach (uint dpi in new uint[] { 96, 144 })
        {
            var alca = Alca(area, dpi);
            var painel = Painel(area, dpi);
            Exigir(alca.Direita == area.Direita);
            Exigir(painel.Direita == area.Direita);
            Exigir(alca.Largura == DipParaPx(AlcaLarguraDip, dpi));
            Exigir(painel.Largura == DipParaPx(PainelLarguraDip, dpi));
            var meio = Interpolar(alca, painel, 0.5);
            Exigir(meio.Direita == area.Direita);
            Exigir(Interpolar(alca, painel, 0) == alca);
            Exigir(Interpolar(alca, painel, 1) == painel);
        }

        var estreita = new Retangulo(0, 0, 100, 80);
        var painelEstreito = Painel(estreita, 96);
        Exigir(painelEstreito.Largura == 100);
        Exigir(painelEstreito.Direita == 100);
        var alcaBaixa = Alca(estreita, 96);
        Exigir(alcaBaixa.Altura == 80);
    }

    static int Misturar(int de, int para, double t) =>
        (int)Math.Round(de + (para - de) * t);

    static void Exigir(bool condicao)
    {
        if (!condicao)
            throw new InvalidOperationException("Posicao.SelfCheck falhou.");
    }
}
