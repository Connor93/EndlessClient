using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EndlessClient.UI.Myra;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Input;
using Myra.Graphics2D.UI;
using XNAControls;
using XNAControls.Input;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Base class that bridges a Myra Window to IXNADialog / IXNAControl.
    /// Myra owns all rendering and input — the IXNAControl lifecycle methods are stubs.
    /// Subclasses build their UI by adding widgets to the Window's Content.
    /// </summary>
    public abstract class MyraDialogAdapter : IXNADialog, ITextInputDialog
    {
        private readonly IMyraUIManager _uiManager;
        private readonly TaskCompletionSource<XNADialogResult> _tcs;

        private bool _disposed;

        protected Window Window { get; }

        public event EventHandler<DialogClosingEventArgs> DialogClosing;
        public event EventHandler DialogClosed;

        /// <summary>
        /// The text entered by the user. Override in subclass if applicable.
        /// </summary>
        public virtual string ResponseText => string.Empty;

        protected MyraDialogAdapter(IMyraUIManager uiManager, string title)
        {
            _uiManager = uiManager;
            _tcs = new TaskCompletionSource<XNADialogResult>();

            Window = new Window
            {
                Title = title
            };

            // Wire Myra's close button to our Close() flow
            Window.CloseButton.Click += (_, _) => Close(XNADialogResult.Cancel);
        }

        /// <summary>
        /// Close the dialog with the specified result.
        /// Fires DialogClosing (cancellable) then DialogClosed.
        /// </summary>
        protected void Close(XNADialogResult result)
        {
            var args = new DialogClosingEventArgs(result);
            DialogClosing?.Invoke(this, args);

            if (args.Cancel)
                return;

            Window.Close();
            _uiManager.UnregisterDialog(this);
            _tcs.TrySetResult(result);
            DialogClosed?.Invoke(this, EventArgs.Empty);
        }

        // === IXNADialog implementation ===

        // IXNADialog.Modal is internal to XNAControls — not implementable here.
        // The default interface implementation handles it.

        public void BringToTop()
        {
            // Myra renders widgets in order — last in the collection draws on top.
            // Remove and re-add to move this window to the front.
            var widgets = _uiManager.Desktop.Widgets;
            if (widgets.Contains(Window))
            {
                widgets.Remove(Window);
                widgets.Add(Window);
            }
        }

        public void CenterInGameView()
        {
            // Myra Windows are centered by default
        }

        public void ShowDialog()
        {
            Window.ShowModal(_uiManager.Desktop);
            _uiManager.RegisterDialog(this);
            BringToTop();
        }

        public Task<XNADialogResult> ShowDialogAsync()
        {
            Window.ShowModal(_uiManager.Desktop);
            _uiManager.RegisterDialog(this);
            BringToTop();
            return _tcs.Task;
        }

        public void Show()
        {
            Window.Show(_uiManager.Desktop);
            _uiManager.RegisterDialog(this);
            BringToTop();
        }

        // === IXNAControl stubs (Myra handles rendering/input) ===

        public bool GameIsActive => true;
        public bool MouseOver
        {
            get
            {
                if (Window == null || !Window.Visible) return false;
                var mousePos = _uiManager.GetLogicalMousePosition();
                // Window.ActualBounds position is stale after the user drags the window.
                // Use Window.Left/Top (which track the moved position) for the origin.
                var actual = Window.ActualBounds;
                var bounds = new Rectangle(
                    Window.Left,
                    Window.Top,
                    actual.Width, actual.Height);
                return bounds.Contains((int)mousePos.X, (int)mousePos.Y);
            }
        }

        public bool MouseOverPreviously => false;
        public Vector2 DrawPosition { get; set; }
        public Vector2 DrawPositionWithParentOffset => DrawPosition;
        public Rectangle DrawArea { get; set; }
        public Rectangle DrawAreaWithParentOffset => DrawArea;
        public IXNAControl ImmediateParent => null;
        public IXNAControl TopParent => null;
        public IReadOnlyList<IXNAControl> ChildControls => Array.Empty<IXNAControl>();
        public IReadOnlyList<IXNAControl> FlattenedChildren => Array.Empty<IXNAControl>();
        public bool KeepInClientWindowBounds { get; set; }

        public event EventHandler<MouseStateExtended> OnMouseOver { add { } remove { } }
        public event EventHandler<MouseStateExtended> OnMouseEnter { add { } remove { } }
        public event EventHandler<MouseStateExtended> OnMouseLeave { add { } remove { } }

        public void AddControlToDefaultGame() { }
        public void SetParentControl(IXNAControl parent) { }
        public void SetControlUnparented() { }
        public void SetScrollWheelHandler(IEventReceiver eventReceiver) { }
        public void SetDrawOrder(int drawOrder) { }

        // === IGameComponent ===
        public void Initialize() { }

        // === IDrawable ===
        public int DrawOrder => 0;
        public bool Visible { get; set; } = true;
        public event EventHandler<EventArgs> DrawOrderChanged { add { } remove { } }
        public event EventHandler<EventArgs> VisibleChanged { add { } remove { } }
        public void Draw(GameTime gameTime) { }

        // === IUpdateable ===
        public int UpdateOrder => 0;
        public bool Enabled { get; set; } = true;
        public event EventHandler<EventArgs> UpdateOrderChanged { add { } remove { } }
        public event EventHandler<EventArgs> EnabledChanged { add { } remove { } }
        public virtual void Update(GameTime gameTime) { }

        // === IEventReceiver ===
        public int ZOrder => 0;
        public Rectangle EventArea => Rectangle.Empty;
        public void PostMessage(EventType eventType, object eventArgs) { }
        public bool SendMessage(EventType eventType, object eventArgs) => false;

        // === IDisposable ===
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Window.Close();
            _uiManager.UnregisterDialog(this);
            _tcs.TrySetResult(XNADialogResult.NO_BUTTON_PRESSED);

            GC.SuppressFinalize(this);
        }
    }
}
