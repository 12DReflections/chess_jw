﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pawn : Piece
{
    public override List<Vector2Int> SelectAvaliableSquares()
    {
        avaliableMoves.Clear();

        // Direction is up and down black and white respectively
        Vector2Int direction = team == TeamColor.White ? Vector2Int.up : Vector2Int.down;
        float range = hasMoved ? 1 : 2; //range 2 first move, otherwise 1
        for (int i = 1; i <= range; i++)
        {
            // cannot move forward if there is any piece in front of pawn
            Vector2Int nextCoords = occupiedSquare + direction * i;
            Piece piece = board.GetPieceOnSquare(nextCoords);
            if (!board.CheckIfCoordinatesAreOnBoard(nextCoords))
                break;
            if (piece == null)
                TryToAddMove(nextCoords);
            else if (piece.IsFromSameTeam(this))
                break;
        }

        // Two take directions on each angle forward
        Vector2Int[] takeDirections = new Vector2Int[] { new Vector2Int(1, direction.y), new Vector2Int(-1, direction.y) };
        for (int i = 0; i < takeDirections.Length; i++)
        {
            // Add take direction to occupied square, can move to available square 
            // if enemy piece is on the square. 
            Vector2Int nextCoords = occupiedSquare + takeDirections[i];
            Piece piece = board.GetPieceOnSquare(nextCoords);
            if (!board.CheckIfCoordinatesAreOnBoard(nextCoords))
                continue;
            if (piece != null && !piece.IsFromSameTeam(this))
            {
                TryToAddMove(nextCoords);
            }
        }
        return avaliableMoves;
    }
}