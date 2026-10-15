namespace TodoSidebar.Janela;

sealed class AtalhoUnico
{
    bool _ligado;

    public bool Ligado => _ligado;

    public bool Registrar(Func<bool> registrar)
    {
        if (_ligado)
            return true;
        _ligado = registrar();
        return _ligado;
    }

    public void Liberar(Action liberar)
    {
        if (!_ligado)
            return;
        // ponytail: UnregisterHotKey antes de registrar de novo; o Windows pode ter soltado o atalho no Resume
        liberar();
        _ligado = false;
    }
}
