using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Services;
using EOLib.Domain.IniEditor;
using EOLib.Graphics;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType]
    public class IniEditorDialogFactory : IIniEditorDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _eoDialogButtonService;
        private readonly IIniEditorActions _iniEditorActions;
        private readonly IIniEditorProvider _iniEditorProvider;
        private readonly IEOMessageBoxFactory _eoMessageBoxFactory;
        private readonly IContentProvider _contentProvider;
        private readonly IHudControlProvider _hudControlProvider;

        public IniEditorDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                      IEODialogButtonService eoDialogButtonService,
                                      IIniEditorActions iniEditorActions,
                                      IIniEditorProvider iniEditorProvider,
                                      IEOMessageBoxFactory eoMessageBoxFactory,
                                      IContentProvider contentProvider,
                                      IHudControlProvider hudControlProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _eoDialogButtonService = eoDialogButtonService;
            _iniEditorActions = iniEditorActions;
            _iniEditorProvider = iniEditorProvider;
            _eoMessageBoxFactory = eoMessageBoxFactory;
            _contentProvider = contentProvider;
            _hudControlProvider = hudControlProvider;
        }

        public IniEditorDialog Create()
        {
            return new IniEditorDialog(_nativeGraphicsManager,
                                       _eoDialogButtonService,
                                       _iniEditorActions,
                                       _iniEditorProvider,
                                       _eoMessageBoxFactory,
                                       _contentProvider,
                                       _hudControlProvider);
        }
    }

    public interface IIniEditorDialogFactory
    {
        IniEditorDialog Create();
    }
}
