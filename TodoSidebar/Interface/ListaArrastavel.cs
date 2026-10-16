using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TodoSidebar.Interface;

readonly record struct Alvo(string Tipo, string? ProjetoId, string Id);

static class ListaArrastavel
{
    const string Formato = "TodoSidebar.Alvo";

    public static Border Envolver(UIElement conteudo, Alvo alvo, bool podeArrastar, Action<Alvo, Alvo, bool> soltar)
    {
        var borda = new Border
        {
            Child = conteudo,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Tag = alvo,
            AllowDrop = podeArrastar
        };
        borda.SetResourceReference(Border.BorderBrushProperty, "Arraste");
        if (!podeArrastar)
            return borda;

        Point? origem = null;
        borda.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is Button or CheckBox or TextBox)
                return;
            origem = e.GetPosition(borda);
        };
        borda.PreviewMouseLeftButtonUp += (_, _) => origem = null;
        borda.PreviewMouseMove += (_, e) =>
        {
            if (origem == null || e.LeftButton != MouseButtonState.Pressed || e.OriginalSource is Button or CheckBox or TextBox)
                return;
            var agora = e.GetPosition(borda);
            if (Math.Abs(agora.X - origem.Value.X) < 8 && Math.Abs(agora.Y - origem.Value.Y) < 8)
                return;
            origem = null;
            DragDrop.DoDragDrop(borda, new DataObject(Formato, alvo), DragDropEffects.Move);
        };
        borda.DragOver += (_, e) =>
        {
            if (!MesmoGrupo(e, alvo, out Alvo _))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.Move;
            bool depois = NaMetadeInferior(borda, e);
            borda.BorderThickness = depois ? new Thickness(0, 0, 0, 2) : new Thickness(0, 2, 0, 0);
            e.Handled = true;
        };
        borda.DragLeave += (_, _) => borda.BorderThickness = new Thickness(0);
        borda.Drop += (_, e) =>
        {
            borda.BorderThickness = new Thickness(0);
            if (!MesmoGrupo(e, alvo, out var origem) || origem == alvo)
            {
                e.Handled = true;
                return;
            }

            soltar(origem, alvo, NaMetadeInferior(borda, e));
            e.Handled = true;
        };
        return borda;
    }

    static bool MesmoGrupo(DragEventArgs e, Alvo destino, out Alvo origem)
    {
        origem = default;
        if (!e.Data.GetDataPresent(Formato))
            return false;
        origem = (Alvo)e.Data.GetData(Formato);
        return origem.Tipo == destino.Tipo && origem.ProjetoId == destino.ProjetoId;
    }

    static bool NaMetadeInferior(Border borda, DragEventArgs e) =>
        e.GetPosition(borda).Y >= borda.ActualHeight / 2;
}
