using System.IO;
using Microsoft.Win32;

namespace TodoSidebar.Janela;

enum EstadoInicio
{
    Desligada,
    Ligada,
    OutroExecutavel,
    Indisponivel
}

sealed class InicioAutomatico
{
    public const string NomeValor = "TodoSidebar";
    const string Chave = @"Software\Microsoft\Windows\CurrentVersion\Run";

    readonly Func<string?> _ler;
    readonly Action<string> _gravar;
    readonly Action _apagar;

    public InicioAutomatico()
        : this(LerRegistro, GravarRegistro, ApagarRegistro)
    {
    }

    public InicioAutomatico(Func<string?> ler, Action<string> gravar, Action apagar)
    {
        _ler = ler;
        _gravar = gravar;
        _apagar = apagar;
    }

    public EstadoInicio Ler(string executavel)
    {
        try
        {
            var atual = _ler();
            if (string.IsNullOrWhiteSpace(atual))
                return EstadoInicio.Desligada;
            return MesmoArquivo(atual, executavel) ? EstadoInicio.Ligada : EstadoInicio.OutroExecutavel;
        }
        catch (Exception)
        {
            return EstadoInicio.Indisponivel;
        }
    }

    public EstadoInicio Ligar(string executavel)
    {
        try
        {
            _gravar(ComAspas(executavel));
            return Ler(executavel);
        }
        catch (Exception)
        {
            return EstadoInicio.Indisponivel;
        }
    }

    public EstadoInicio Desligar(string executavel)
    {
        try
        {
            _apagar();
            return Ler(executavel);
        }
        catch (Exception)
        {
            return EstadoInicio.Indisponivel;
        }
    }

    public static string ExecutavelAtual()
    {
        var aoLado = Path.Combine(AppContext.BaseDirectory, "TodoSidebar.exe");
        if (File.Exists(aoLado))
            return Path.GetFullPath(aoLado);
        var processo = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processo) && File.Exists(processo))
            return Path.GetFullPath(processo);
        return aoLado;
    }

    public static string ComAspas(string caminho) => "\"" + caminho + "\"";

    public static bool MesmoArquivo(string valor, string executavel)
    {
        var a = valor.Trim().Trim('"');
        var b = executavel.Trim().Trim('"');
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    static string? LerRegistro()
    {
        using var chave = Registry.CurrentUser.OpenSubKey(Chave, false);
        return chave?.GetValue(NomeValor) as string;
    }

    static void GravarRegistro(string valor)
    {
        using var chave = Registry.CurrentUser.CreateSubKey(Chave, true);
        chave.SetValue(NomeValor, valor);
    }

    static void ApagarRegistro()
    {
        using var chave = Registry.CurrentUser.OpenSubKey(Chave, true);
        chave?.DeleteValue(NomeValor, false);
    }
}
