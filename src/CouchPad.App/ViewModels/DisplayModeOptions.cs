using CouchPad.Core.Display;

namespace CouchPad.App.ViewModels;

/// <summary>Resolução escolhida para a TV; <c>Mode</c> nulo representa automático.</summary>
public sealed record TvModeOption(DisplayModeOption? Mode, string Label);

/// <summary>Taxa de atualização escolhida; a unidade Hz é sufixo, não tradução.</summary>
public sealed record TvRefreshOption(int Hz)
{
    public string Label => $"{Hz} Hz";
}

/// <summary>Escala do Windows; <c>Percent</c> nulo representa automático.</summary>
public sealed record TvScaleOption(int? Percent, string Label);
