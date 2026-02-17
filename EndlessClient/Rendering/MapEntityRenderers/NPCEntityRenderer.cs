using System.Collections.Generic;
using EndlessClient.Rendering.Map;
using EndlessClient.Rendering.NPC;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EndlessClient.Rendering.MapEntityRenderers
{
    public class NPCEntityRenderer : BaseMapEntityRenderer
    {
        private readonly INPCRendererProvider _npcRendererProvider;
        private readonly ICurrentMapStateProvider _currentMapStateProvider;

        public NPCEntityRenderer(ICharacterProvider characterProvider,
                                 IGridDrawCoordinateCalculator gridDrawCoordinateCalculator,
                                 IClientWindowSizeProvider clientWindowSizeProvider,
                                 INPCRendererProvider npcRendererProvider,
                                 ICurrentMapStateProvider currentMapStateProvider)
            : base(characterProvider, gridDrawCoordinateCalculator, clientWindowSizeProvider)
        {
            _npcRendererProvider = npcRendererProvider;
            _currentMapStateProvider = currentMapStateProvider;
        }

        public override MapRenderLayer RenderLayer => MapRenderLayer.Npc;

        protected override int RenderDistance => 16;

        protected override bool ElementExistsAt(int row, int col)
        {
            var coordinate = new MapCoordinate(col, row);
            return _currentMapStateProvider.NPCs.ContainsKey(coordinate) || _npcRendererProvider.DyingNPCs.ContainsKey(coordinate);
        }

        public override void RenderElementAt(SpriteBatch spriteBatch, int row, int col, int alpha, Vector2 additionalOffset = default)
        {
            var coordinate = new MapCoordinate(col, row);

            var indices = new List<int>();

            if (_npcRendererProvider.DyingNPCs.TryGetValue(coordinate, out var dyingIndex))
                indices.Add(dyingIndex);

            if (_currentMapStateProvider.NPCs.TryGetValues(coordinate, out var npcs))
            {
                foreach (var npc in npcs)
                    indices.Add(npc.Index);
            }

            foreach (var index in indices)
            {
                if (!_npcRendererProvider.NPCRenderers.ContainsKey(index) ||
                    _npcRendererProvider.NPCRenderers[index] == null)
                    continue;

                _npcRendererProvider.NPCRenderers[index].DrawToSpriteBatch(spriteBatch);
            }
        }
    }
}
