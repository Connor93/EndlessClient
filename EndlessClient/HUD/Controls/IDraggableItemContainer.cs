using XNAControls;

namespace EndlessClient.HUD.Controls
{
    public interface IDraggableItemContainer : IXNAControl
    {
        bool NoItemsDragging();

        Microsoft.Xna.Framework.Point TransformMousePosition(Microsoft.Xna.Framework.Point position);
    }
}
