using System.IO;
using System.Windows;
using TodoSidebar.Dados;
using TodoSidebar.Interface;
using TodoSidebar.Janela;

namespace TodoSidebar;

public partial class App : Application
{
    Mutex? _instancia;
    EventWaitHandle? _sinal;
    Thread? _escuta;
    volatile bool _parar;
    Banco? _banco;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        FaseSidebar.SelfCheck();
        Posicao.SelfCheck();

        var mutex = new Mutex(true, @"Local\TodoSidebar", out bool criada);
        _sinal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\TodoSidebarSinal");
        if (!criada)
        {
            mutex.Dispose();
            _sinal.Set();
            _sinal.Dispose();
            Shutdown();
            return;
        }

        _instancia = mutex;
        var caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TodoSidebar",
            "todo.db");
        _banco = new Banco(caminho);
        try
        {
            _banco.Abrir();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Tarefas");
            Shutdown();
            return;
        }

        var janela = new JanelaSidebar();
        janela.DefinirPainel(new PainelTarefas(_banco));
        MainWindow = janela;
        janela.Show();

        _escuta = new Thread(Escutar) { IsBackground = true };
        _escuta.Start();
    }

    void Escutar()
    {
        while (_sinal != null && _sinal.WaitOne())
        {
            if (_parar)
                return;
            Dispatcher.Invoke(() =>
            {
                if (MainWindow is JanelaSidebar janela)
                    janela.Alternar();
            });
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _parar = true;
        try { _sinal?.Set(); } catch (ObjectDisposedException) { }
        _sinal?.Dispose();
        _banco?.Dispose();
        _instancia?.ReleaseMutex();
        _instancia?.Dispose();
        base.OnExit(e);
    }
}
