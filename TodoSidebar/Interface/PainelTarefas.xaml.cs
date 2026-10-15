using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TodoSidebar.Dados;
using TodoSidebar.Janela;
using TodoSidebar.Registros;

namespace TodoSidebar.Interface;

public partial class PainelTarefas : UserControl
{
    static readonly Brush Fundo = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1C));
    static readonly Brush Texto = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
    static readonly Brush Secundario = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
    static readonly Brush Concluida = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E));
    static readonly Brush Borda = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));

    readonly Banco _banco;
    readonly InicioAutomatico _inicio = new();
    string? _edicao;
    string? _confirmarTarefa;
    string? _confirmarProjeto;

    public PainelTarefas(Banco banco)
    {
        InitializeComponent();
        _banco = banco;
        CampoAvulsa.Tag = "Nova tarefa avulsa";
        CampoProjeto.Tag = "Novo projeto";
        MostrarDica(CampoAvulsa);
        MostrarDica(CampoProjeto);
        CampoAvulsa.GotKeyboardFocus += Campo_GotFocus;
        CampoAvulsa.LostKeyboardFocus += Campo_LostFocus;
        CampoProjeto.GotKeyboardFocus += Campo_GotFocus;
        CampoProjeto.LostKeyboardFocus += Campo_LostFocus;
        Loaded += (_, _) => AtualizarInicio();
        Redesenhar();
    }

    void InicioWindows_Click(object sender, RoutedEventArgs e)
    {
        var exe = InicioAutomatico.ExecutavelAtual();
        var estado = InicioWindows.IsChecked == true
            ? _inicio.Ligar(exe)
            : _inicio.Desligar(exe);
        AtualizarInicio();
        if (estado == EstadoInicio.Indisponivel)
        {
            AvisoInicio.Text = "Não foi possível alterar a inicialização automática.";
            AvisoInicio.Visibility = Visibility.Visible;
        }
    }

    void AtualizarInicio()
    {
        var exe = InicioAutomatico.ExecutavelAtual();
        var estado = _inicio.Ler(exe);
        InicioWindows.IsChecked = estado == EstadoInicio.Ligada;
        InicioWindows.IsEnabled = estado != EstadoInicio.Indisponivel;
        if (estado == EstadoInicio.Indisponivel)
        {
            AvisoInicio.Text = "Não foi possível ler a inicialização automática.";
            AvisoInicio.Visibility = Visibility.Visible;
            return;
        }

        if (estado == EstadoInicio.OutroExecutavel)
        {
            AvisoInicio.Text = "Há outra entrada de inicialização. Marcar substitui por este programa.";
            AvisoInicio.Visibility = Visibility.Visible;
            return;
        }

        AvisoInicio.Visibility = Visibility.Collapsed;
    }

    public bool TratarEsc()
    {
        if (_edicao != null || _confirmarTarefa != null || _confirmarProjeto != null)
        {
            _edicao = null;
            _confirmarTarefa = null;
            _confirmarProjeto = null;
            Redesenhar();
            return true;
        }

        if (Termo().Length > 0)
        {
            CampoBusca.Text = "";
            return true;
        }

        return false;
    }

    string Termo() => CampoBusca.Text.Trim();

    bool Filtrando() => Termo().Length > 0;

    void CampoBusca_TextChanged(object sender, TextChangedEventArgs e) =>
        Redesenhar();

    void Redesenhar()
    {
        Lista.Children.Clear();
        bool filtrando = Filtrando();
        if (filtrando)
            Lista.Children.Add(Rotulo("Limpe a busca para reordenar."));

        var avulsas = _banco.ListarAvulsas().Where(t => Serve(t.Titulo)).ToList();
        if (!filtrando || avulsas.Count > 0)
        {
            Lista.Children.Add(Rotulo("Avulsas"));
            foreach (var tarefa in avulsas)
                Lista.Children.Add(LinhaTarefa(tarefa, null, filtrando));
        }

        Lista.Children.Add(Rotulo("Projetos"));
        foreach (var projeto in _banco.ListarProjetos())
        {
            var tarefas = _banco.ListarDoProjeto(projeto.Id).Where(t => Serve(t.Titulo)).ToList();
            if (filtrando && tarefas.Count == 0)
                continue;
            Lista.Children.Add(LinhaProjeto(projeto, filtrando));
            if (!filtrando && !projeto.Expandido)
                continue;
            foreach (var tarefa in tarefas)
                Lista.Children.Add(LinhaTarefa(tarefa, projeto.Nome, filtrando));
            if (!filtrando)
                Lista.Children.Add(CampoNovaDoProjeto(projeto.Id));
        }
    }

    bool Serve(string titulo) =>
        !Filtrando() || titulo.Contains(Termo(), StringComparison.OrdinalIgnoreCase);

    TextBlock Rotulo(string texto) => new()
    {
        Text = texto,
        Foreground = Secundario,
        Margin = new Thickness(0, 12, 0, 6)
    };

    UIElement LinhaTarefa(Tarefa tarefa, string? nomeProjeto, bool filtrando)
    {
        var linha = new DockPanel { MinHeight = 36, LastChildFill = true };
        if (_confirmarTarefa == tarefa.Id)
        {
            linha.Children.Add(Botao("Sim", () => Executar(() =>
            {
                _banco.ExcluirTarefa(tarefa.Id);
                _confirmarTarefa = null;
            }), Dock.Right));
            linha.Children.Add(Botao("Não", () => { _confirmarTarefa = null; Redesenhar(); }, Dock.Right));
            linha.Children.Add(new TextBlock
            {
                Text = "Excluir esta tarefa?",
                Foreground = Texto,
                VerticalAlignment = VerticalAlignment.Center
            });
            return EnvolverTarefa(linha, tarefa, filtrando);
        }

        var caixa = new CheckBox
        {
            IsChecked = tarefa.Concluida,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        caixa.Click += (_, _) => Executar(() => _banco.DefinirConclusao(tarefa.Id, caixa.IsChecked == true));
        DockPanel.SetDock(caixa, Dock.Left);
        linha.Children.Add(caixa);
        linha.Children.Add(Botao("Excluir", () => { _confirmarTarefa = tarefa.Id; _confirmarProjeto = null; Redesenhar(); }, Dock.Right));

        if (_edicao == "t:" + tarefa.Id)
        {
            var campo = CampoEdicao(tarefa.Titulo, texto => Executar(() =>
            {
                _banco.RenomearTarefa(tarefa.Id, texto);
                _edicao = null;
            }));
            linha.Children.Add(campo);
            Dispatcher.BeginInvoke(() => { campo.Focus(); campo.SelectAll(); });
            return EnvolverTarefa(linha, tarefa, filtrando);
        }

        var bloco = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        bloco.Children.Add(new TextBlock
        {
            Text = tarefa.Titulo,
            Foreground = tarefa.Concluida ? Concluida : Texto,
            TextDecorations = tarefa.Concluida ? TextDecorations.Strikethrough : null,
            TextWrapping = TextWrapping.Wrap
        });
        if (filtrando)
            bloco.Children.Add(new TextBlock
            {
                Text = nomeProjeto == null ? "Avulsa" : "Projeto: " + nomeProjeto,
                Foreground = Secundario,
                FontSize = 11
            });
        bloco.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2)
                return;
            _edicao = "t:" + tarefa.Id;
            Redesenhar();
        };
        linha.Children.Add(bloco);
        return EnvolverTarefa(linha, tarefa, filtrando);
    }

    UIElement EnvolverTarefa(UIElement linha, Tarefa tarefa, bool filtrando) =>
        ListaArrastavel.Envolver(linha, new Alvo("tarefa", tarefa.ProjetoId, tarefa.Id), !filtrando, Soltar);

    UIElement LinhaProjeto(Projeto projeto, bool filtrando)
    {
        var linha = new DockPanel { MinHeight = 36 };
        if (_confirmarProjeto == projeto.Id)
            return ConfirmacaoProjeto(projeto);

        linha.Children.Add(Botao(projeto.Expandido ? "Recolher" : "Expandir", () => Executar(() =>
            _banco.DefinirExpandido(projeto.Id, !projeto.Expandido)), Dock.Left));
        linha.Children.Add(Botao("Excluir", () => { _confirmarProjeto = projeto.Id; _confirmarTarefa = null; Redesenhar(); }, Dock.Right));

        if (_edicao == "p:" + projeto.Id)
        {
            var campo = CampoEdicao(projeto.Nome, texto => Executar(() =>
            {
                _banco.RenomearProjeto(projeto.Id, texto);
                _edicao = null;
            }));
            linha.Children.Add(campo);
            Dispatcher.BeginInvoke(() => { campo.Focus(); campo.SelectAll(); });
            return EnvolverProjeto(linha, projeto, filtrando);
        }

        var nome = new TextBlock
        {
            Text = projeto.Nome,
            Foreground = Texto,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(8, 0, 0, 0)
        };
        nome.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2)
                return;
            _edicao = "p:" + projeto.Id;
            Redesenhar();
        };
        linha.Children.Add(nome);
        return EnvolverProjeto(linha, projeto, filtrando);
    }

    UIElement ConfirmacaoProjeto(Projeto projeto)
    {
        var resumo = _banco.ResumoExclusao(projeto.Id);
        var painel = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
        painel.Children.Add(new TextBlock
        {
            Text = TextoExclusao(resumo),
            Foreground = Texto,
            TextWrapping = TextWrapping.Wrap
        });
        var botoes = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        botoes.Children.Add(Botao("Excluir", () => Executar(() =>
        {
            _banco.ExcluirProjeto(projeto.Id);
            _confirmarProjeto = null;
        }), Dock.Right));
        botoes.Children.Add(Botao("Cancelar", () => { _confirmarProjeto = null; Redesenhar(); }, Dock.Right));
        painel.Children.Add(botoes);
        return painel;
    }

    static string TextoExclusao(ResumoExclusao r)
    {
        string concluidas = r.Concluidas switch
        {
            0 => "Nenhuma tarefa concluída será excluída.",
            1 => "1 tarefa concluída será excluída.",
            _ => r.Concluidas + " tarefas concluídas serão excluídas."
        };
        string abertas = r.Abertas switch
        {
            0 => "Nenhuma tarefa em aberto será mantida como tarefa avulsa.",
            1 => "1 tarefa em aberto será mantida como tarefa avulsa.",
            _ => r.Abertas + " tarefas em aberto serão mantidas como tarefas avulsas."
        };
        return "Excluir o projeto " + r.Nome + "?\n\n- " + concluidas + "\n- " + abertas
            + "\n\nEsta ação não poderá ser desfeita.";
    }

    UIElement EnvolverProjeto(UIElement linha, Projeto projeto, bool filtrando) =>
        ListaArrastavel.Envolver(linha, new Alvo("projeto", null, projeto.Id), !filtrando, Soltar);

    void Soltar(Alvo origem, Alvo destino, bool depois)
    {
        Executar(() =>
        {
            if (origem.Tipo == "projeto")
            {
                var ids = _banco.ListarProjetos().Select(p => p.Id).ToList();
                Mover(ids, origem.Id, destino.Id, depois);
                _banco.ReordenarProjetos(ids);
                return;
            }

            var grupo = origem.ProjetoId == null
                ? _banco.ListarAvulsas().Select(t => t.Id).ToList()
                : _banco.ListarDoProjeto(origem.ProjetoId).Select(t => t.Id).ToList();
            Mover(grupo, origem.Id, destino.Id, depois);
            if (origem.ProjetoId == null)
                _banco.ReordenarAvulsas(grupo);
            else
                _banco.ReordenarDoProjeto(origem.ProjetoId, grupo);
        });
    }

    static void Mover(List<string> ids, string origem, string destino, bool depois)
    {
        ids.Remove(origem);
        int indice = ids.IndexOf(destino);
        if (indice < 0)
            return;
        ids.Insert(depois ? indice + 1 : indice, origem);
    }

    UIElement CampoNovaDoProjeto(string projetoId)
    {
        var campo = new TextBox
        {
            MinHeight = 36,
            Margin = new Thickness(24, 4, 0, 8),
            Padding = new Thickness(8, 6, 8, 6),
            Background = Fundo,
            Foreground = Secundario,
            BorderBrush = Borda,
            CaretBrush = Texto,
            Text = "Nova tarefa neste projeto"
        };
        campo.GotKeyboardFocus += Campo_GotFocus;
        campo.LostKeyboardFocus += Campo_LostFocus;
        campo.Tag = "Nova tarefa neste projeto";
        campo.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            Executar(() => _banco.CriarNoProjeto(projetoId, campo.Text));
            campo.Text = "";
            e.Handled = true;
        };
        return campo;
    }

    Button Botao(string texto, Action acao, Dock dock)
    {
        var botao = new Button
        {
            Content = texto,
            MinHeight = 36,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(10, 6, 10, 6)
        };
        DockPanel.SetDock(botao, dock);
        botao.Click += (_, _) => acao();
        return botao;
    }

    TextBox CampoEdicao(string valor, Action<string> confirmar)
    {
        var campo = new TextBox
        {
            Text = valor,
            MinHeight = 36,
            Padding = new Thickness(8, 6, 8, 6),
            Background = Fundo,
            Foreground = Texto,
            BorderBrush = Borda,
            CaretBrush = Texto
        };
        campo.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                confirmar(campo.Text);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _edicao = null;
                Redesenhar();
                e.Handled = true;
            }
        };
        return campo;
    }

    void CampoAvulsa_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        Executar(() => _banco.CriarAvulsa(CampoAvulsa.Text));
        CampoAvulsa.Text = "";
        e.Handled = true;
    }

    void CampoProjeto_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        Executar(() => _banco.CriarProjeto(CampoProjeto.Text));
        CampoProjeto.Text = "";
        e.Handled = true;
    }

    void Campo_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox campo && campo.Text == Convert.ToString(campo.Tag))
        {
            campo.Text = "";
            campo.Foreground = Texto;
        }
    }

    void Campo_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox campo)
            MostrarDica(campo);
    }

    static void MostrarDica(TextBox campo)
    {
        if (campo.Text.Trim().Length != 0)
            return;
        campo.Text = Convert.ToString(campo.Tag) ?? "";
        campo.Foreground = Secundario;
    }

    void Executar(Action acao)
    {
        try
        {
            acao();
            Aviso.Visibility = Visibility.Collapsed;
            Redesenhar();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Aviso.Text = ex.Message;
            Aviso.Visibility = Visibility.Visible;
        }
    }
}
