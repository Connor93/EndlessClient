using System.Collections.Generic;
using System.Linq;
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
            return _currentMapStateProvider.NPCs.ContainsKey(coordinate)
                || _npcRendererProvider.DyingNPCs.ContainsKey(coordinate)
                || _npcRendererProvider.NPCRenderers.Values.Any(r => !r.IsDead && r.NPC.X == col && r.NPC.Y == row);
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

            // Also include NPCs whose renderers are at this coordinate but whose
            // domain position may have shifted (e.g. mid-walk coordinate change).
            // This prevents NPCs from disappearing when the map render target is
            // rebuilt and the diagonal grid iteration misses them at both old and
            // new domain coordinates.
            foreach (var kvp in _npcRendererProvider.NPCRenderers)
            {
                if (!kvp.Value.IsDead && !indices.Contains(kvp.Key)
                    && kvp.Value.NPC.X == col && kvp.Value.NPC.Y == row)
                {
                    indices.Add(kvp.Key);
                }
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
