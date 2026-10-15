namespace TodoSidebar.Registros;

public readonly record struct Tarefa(
    string Id,
    string? ProjetoId,
    string Titulo,
    bool Concluida,
    int Ordem,
    string CriadaEm,
    string AtualizadaEm);
