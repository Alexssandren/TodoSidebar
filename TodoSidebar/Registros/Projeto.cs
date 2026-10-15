namespace TodoSidebar.Registros;

public readonly record struct Projeto(
    string Id,
    string Nome,
    int Ordem,
    bool Expandido,
    string CriadoEm,
    string AtualizadoEm);
