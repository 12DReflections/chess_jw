using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pawn : Piece
{
    public override List<Vector2Int> SelectAvaliableSquares()
    {
        avaliableMoves.Clear();
        avaliableMoves.Add(occupiedSquare + new Vector2Int(0, 1));
        return avaliableMoves;

    }
}
