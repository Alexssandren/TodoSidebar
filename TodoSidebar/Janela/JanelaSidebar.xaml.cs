using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace TodoSidebar.Janela;

public partial class JanelaSidebar : Window
{
    const int EsperaAbrirMs = 200;
    const int EsperaRecolherMs = 400;
    const int AnimacaoMs = 160;

    readonly DispatcherTimer _esperaAbrir;
    readonly DispatcherTimer _esperaRecolher;
    readonly DispatcherTimer _animacao;
    readonly Stopwatch _relogioAnimacao = new();
    readonly AtalhoUnico _atalho = new();

    IntPtr _hwnd;
    IntPtr _janelaAnterior;
    Fase _fase = Fase.Recolhida;
    Retangulo _pedido;
    Retangulo _animDe;
    Retangulo _animPara;
    bool _animandoParaAberta;
    bool _reposicionarPendente;
    uint _dpi;

    public JanelaSidebar()
    {
        InitializeComponent();
        var area = SystemParameters.WorkArea;
        Width = Posicao.AlcaLarguraDip;
        Height = Posicao.AlcaAlturaDip;
        Left = area.Right - Posicao.AlcaLarguraDip;
        Top = area.Top + (area.Height - Posicao.AlcaAlturaDip) / 2;

        _esperaAbrir = CriarEspera(EsperaAbrirMs, AbrirPorEspera);
        _esperaRecolher = CriarEspera(EsperaRecolherMs, RecolherPorEspera);
        _animacao = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animacao.Tick += Animacao_Tick;
        AvisoAtalho.Text = "Atalho: Ctrl+Alt+Espaço";
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        Win32Janela.Preparar(_hwnd);
        if (!_atalho.Registrar(() => Win32Janela.RegistrarAtalho(_hwnd)))
            AvisoAtalho.Text = "Atalho não registrado";
        SystemEvents.PowerModeChanged += Energia;

        AplicarRetangulo(RetanguloAlvo(false));
        if (Win32Janela.CursorDentro(_hwnd))
            AplicarFase(FaseSidebar.AoEntrar(_fase), false);
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.PowerModeChanged -= Energia;
        _atalho.Liberar(() => Win32Janela.LiberarAtalho(_hwnd));
        base.OnClosed(e);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Janela.WmWindowPosChanging && _pedido.Largura > 0)
            Win32Janela.ForcarRetangulo(lParam, _pedido);

        if (Win32Janela.TamanhoMinimoZero(msg, lParam))
        {
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == Win32Janela.WmHotkey && wParam == new IntPtr(Win32Janela.IdAtalho))
        {
            AlternarPeloAtalho();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == Win32Janela.WmDisplayChange
            || msg == Win32Janela.WmDpiChanged
            || (msg == Win32Janela.WmSettingChange && wParam == new IntPtr(Win32Janela.SpiSetWorkArea)))
        {
            PedirReposicionar();
        }

        return IntPtr.Zero;
    }

    void Energia(object sender, PowerModeChangedEventArgs e)
    {
        var modo = e.Mode;
        Dispatcher.BeginInvoke(() =>
        {
            if (modo == PowerModes.Suspend)
                Suspender();
            else if (modo == PowerModes.Resume)
                Retomar();
        });
    }

    void Suspender()
    {
        _esperaAbrir.Stop();
        _esperaRecolher.Stop();
        _animacao.Stop();
    }

    void Retomar()
    {
        Suspender();
        if (_fase == Fase.AguardandoAbrir)
            _fase = Fase.Recolhida;
        if (_fase == Fase.AguardandoRecolher)
            _fase = Fase.Aberta;
        RestaurarAtalho();
        Reposicionar(true);
        if (Win32Janela.CursorDentro(_hwnd))
            AplicarFase(FaseSidebar.AoEntrar(_fase), false);
    }

    void RestaurarAtalho()
    {
        if (_hwnd == IntPtr.Zero)
            return;
        _atalho.Liberar(() => Win32Janela.LiberarAtalho(_hwnd));
        if (!_atalho.Registrar(() => Win32Janela.RegistrarAtalho(_hwnd)))
            AvisoAtalho.Text = "Atalho não registrado";
        else if (AvisoAtalho.Text == "Atalho não registrado")
            AvisoAtalho.Text = "Atalho: Ctrl+Alt+Espaço";
    }

    void PedirReposicionar()
    {
        if (_reposicionarPendente)
            return;
        _reposicionarPendente = true;
        Dispatcher.BeginInvoke(() =>
        {
            _reposicionarPendente = false;
            Reposicionar(false);
        });
    }

    void Janela_MouseEnter(object sender, MouseEventArgs e) =>
        AplicarFase(FaseSidebar.AoEntrar(_fase), false);

    void Janela_MouseLeave(object sender, MouseEventArgs e)
    {
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            Mouse.Capture(this);
            return;
        }

        AplicarFase(FaseSidebar.AoSair(_fase, false), false);
    }

    void Janela_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured == this)
            Mouse.Capture(null);

        if (_fase == Fase.Aberta && !Win32Janela.CursorDentro(_hwnd))
            AplicarFase(FaseSidebar.AoSair(_fase, false), false);
    }

    public void DefinirPainel(UIElement painel) =>
        Conteudo.Content = painel;

    public void Alternar() =>
        AlternarPeloAtalho();

    void Janela_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (!IsActive)
            return;
        if (Conteudo.Content is Interface.PainelTarefas painel && painel.TratarEsc())
        {
            e.Handled = true;
            return;
        }

        AplicarFase(Fase.Recolhida, false);
        e.Handled = true;
    }

    void Recolher_Click(object sender, RoutedEventArgs e) =>
        AplicarFase(Fase.Recolhida, false);

    void Fechar_Click(object sender, RoutedEventArgs e) =>
        Close();

    void AbrirPorEspera(object? sender, EventArgs e)
    {
        _esperaAbrir.Stop();
        if (_fase != Fase.AguardandoAbrir)
            return;
        if (!Win32Janela.CursorDentro(_hwnd))
        {
            AplicarFase(Fase.Recolhida, false);
            return;
        }

        AplicarFase(Fase.Aberta, false);
    }

    void RecolherPorEspera(object? sender, EventArgs e)
    {
        _esperaRecolher.Stop();
        if (Win32Janela.CursorDentro(_hwnd))
        {
            AplicarFase(Fase.Aberta, false);
            return;
        }

        AplicarFase(FaseSidebar.AoEsgotarFechamento(_fase), false);
    }

    void AlternarPeloAtalho()
    {
        var nova = FaseSidebar.AoAtalho(_fase);
        if (nova == Fase.Aberta)
            Win32Janela.TrazerParaDesktopDaJanelaFrente(_hwnd);
        AplicarFase(nova, ativar: nova == Fase.Aberta);
    }

    void AplicarFase(Fase nova, bool ativar)
    {
        if (nova == _fase && !ativar)
            return;

        _fase = nova;
        _esperaAbrir.Stop();
        _esperaRecolher.Stop();

        if (nova == Fase.AguardandoAbrir)
            _esperaAbrir.Start();
        if (nova == Fase.AguardandoRecolher)
            _esperaRecolher.Start();
        if (nova is Fase.Aberta or Fase.AguardandoRecolher)
            AceitarClique();
        if (nova == Fase.Recolhida)
            SoltarFoco();
        if (ativar)
            Ativar();

        AnimarPara(nova is Fase.Aberta or Fase.AguardandoRecolher);
    }

    void AceitarClique()
    {
        var frente = Win32Janela.JanelaDaFrente();
        if (frente != IntPtr.Zero && frente != _hwnd)
            _janelaAnterior = frente;
        Win32Janela.DefinirSemAtivar(_hwnd, false);
    }

    void Ativar()
    {
        AceitarClique();
        Win32Janela.Ativar(_hwnd);
    }

    void SoltarFoco()
    {
        bool somosFrente = Win32Janela.JanelaDaFrente() == _hwnd;
        var anterior = _janelaAnterior;
        Win32Janela.DefinirSemAtivar(_hwnd, true);
        if (somosFrente && Win32Janela.JanelaExiste(anterior) && anterior != _hwnd)
            Win32Janela.Ativar(anterior);
        _janelaAnterior = IntPtr.Zero;
    }

    void Reposicionar(bool forcar)
    {
        if (_hwnd == IntPtr.Zero)
            return;

        _animacao.Stop();
        bool aberta = _fase is Fase.Aberta or Fase.AguardandoRecolher;
        var alvo = RetanguloAlvo(aberta);
        uint dpi = Win32Janela.Dpi();
        if (!forcar && alvo == _pedido && dpi == _dpi)
        {
            MostrarPainel(aberta);
            return;
        }

        AplicarRetangulo(alvo);
        MostrarPainel(aberta);
    }

    void AnimarPara(bool aberta)
    {
        var destino = RetanguloAlvo(aberta);
        var atual = Win32Janela.RetanguloAtual(_hwnd);
        if (atual == destino)
        {
            _animacao.Stop();
            AplicarRetangulo(destino);
            MostrarPainel(aberta);
            return;
        }

        _animDe = atual;
        _animPara = destino;
        _animandoParaAberta = aberta;
        _relogioAnimacao.Restart();
        MostrarPainel(true);
        if (!_animacao.IsEnabled)
            _animacao.Start();
    }

    void Animacao_Tick(object? sender, EventArgs e)
    {
        double t = _relogioAnimacao.Elapsed.TotalMilliseconds / AnimacaoMs;
        if (t >= 1)
        {
            _animacao.Stop();
            AplicarRetangulo(_animPara);
            MostrarPainel(_animandoParaAberta);
            return;
        }

        double suave = 1 - Math.Pow(1 - t, 3);
        var quadro = Posicao.Interpolar(_animDe, _animPara, suave);
        AplicarRetangulo(quadro);
        MostrarPainel(true);
    }

    Retangulo RetanguloAlvo(bool aberta)
    {
        var area = Win32Janela.AreaUtil();
        uint dpi = Win32Janela.Dpi();
        return aberta ? Posicao.Painel(area, dpi) : Posicao.Alca(area, dpi);
    }

    void AplicarRetangulo(Retangulo retangulo)
    {
        _pedido = retangulo;
        uint dpi = Win32Janela.Dpi();
        _dpi = dpi;
        Left = Posicao.PxParaDip(retangulo.X, dpi);
        Top = Posicao.PxParaDip(retangulo.Y, dpi);
        Width = Math.Max(1, Posicao.PxParaDip(retangulo.Largura, dpi));
        Height = Math.Max(1, Posicao.PxParaDip(retangulo.Altura, dpi));
        if (_hwnd != IntPtr.Zero)
            Win32Janela.MoverTopo(_hwnd, retangulo);
    }

    void MostrarPainel(bool mostrar)
    {
        PainelVisual.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
        AlcaVisual.Visibility = mostrar ? Visibility.Collapsed : Visibility.Visible;
    }

    DispatcherTimer CriarEspera(int milissegundos, EventHandler aoEsgotar)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milissegundos) };
        timer.Tick += aoEsgotar;
        return timer;
    }
}
