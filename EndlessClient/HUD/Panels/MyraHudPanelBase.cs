using System;
using EndlessClient.UI.Myra;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace EndlessClient.HUD.Panels
{
    /// <summary>
    /// Base class that bridges a Myra Window to IHudPanel.
    /// Analogous to MyraDialogAdapter but for persistent, non-modal HUD panels.
    /// Myra owns all rendering, input, and dragging — subclasses build UI by setting Window.Content.
    /// </summary>
    public abstract class MyraHudPanelBase : DrawableGameComponent, IHudPanel
    {
        private readonly IMyraUIManager _uiManager;

        private bool _disposed;

        protected Window Window { get; }
        protected IMyraUIManager UIManager => _uiManager;

        public event Action Activated;
        public event Action DragCompleted;

        public bool IsBeingDragged { get; private set; }

        public Vector2 DrawPosition
        {
            get => new Vector2(Window.Left, Window.Top);
            set
            {
                Window.Left = (int)value.X;
                Window.Top = (int)value.Y;
            }
        }

        public Rectangle DrawArea
        {
            get => new Rectangle(Window.Left, Window.Top, Window.ActualBounds.Width, Window.ActualBounds.Height);
            set
            {
                Window.Left = value.X;
                Window.Top = value.Y;
                Window.Width = value.Width;
                Window.Height = value.Height;
            }
        }

        new public bool Visible
        {
            get => Window.Visible;
            set => Window.Visible = value;
        }

        new public int DrawOrder
        {
            get => base.DrawOrder;
            set => base.DrawOrder = value;
        }

        public int UpdateOrder
        {
            get => base.UpdateOrder;
            set { /* DrawableGameComponent doesn't expose a setter; panels rely on Myra z-order */ }
        }

        protected MyraHudPanelBase(Game game, IMyraUIManager uiManager, string title)
            : base(game)
        {
            _uiManager = uiManager;

            Window = new Window
            {
                Title = title,
                DragDirection = DragDirection.Both,
                Visible = false,
            };

            // Make the entire window draggable, not just the title bar
            Window.DragHandle = Window;

            // Intercept close to hide instead of destroy (panels are persistent)
            Window.Closing += (_, e) =>
            {
                e.Cancel = true;
                Window.Visible = false;
            };

            // Raise Activated when the window is clicked
            Window.TouchDown += (_, _) =>
            {
                Activated?.Invoke();
                Window.BringToFront();
            };

            // Fire DragCompleted on touch up so position can be persisted
            Window.TouchUp += (_, _) =>
            {
                DragCompleted?.Invoke();
            };
        }

        public override void Initialize()
        {
            _uiManager.Desktop.Widgets.Add(Window);
            base.Initialize();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _uiManager.Desktop.Widgets.Remove(Window);
            }
            base.Dispose(disposing);
        }
    }
}
