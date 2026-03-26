using RogueBlockBlast.Content;
using RogueBlockBlast.Core;

namespace RogueBlockBlast.Game
{
    public static class PieceFactory
    {
        public static PieceDefinition Create(ShapeSO so)
        {
            return new PieceDefinition(
                so.Id,
                so.Cells,
                so.BlockColor,
                so.GetCurrentTileValue()   // upgrade dahil tile değeri
            );
        }
    }
}