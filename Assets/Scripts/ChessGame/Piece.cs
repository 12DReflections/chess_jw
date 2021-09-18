﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MaterialSetter))]
// [RequireComponent(typeof(IObjectTweener))]
public abstract class Piece : MonoBehaviour
{
	[SerializeField] private MaterialSetter materialSetter;
	public Board board { protected get; set; }
	public Vector2Int occupiedSquare { get; set; }
	public TeamColor team { get; set; }
	public bool hasMoved { get; private set; }
	public List<Vector2Int> avaliableMoves;

	// private IObjectTweener tweener;

	public abstract List<Vector2Int> SelectAvaliableSquares();

	private void Awake()
	{
		avaliableMoves = new List<Vector2Int>();
        //tweener = GetComponent<IObjectTweener>();
        materialSetter = GetComponent<MaterialSetter>();
		hasMoved = false;
	}

	public void SetMaterial(Material selectedMaterial)
	{
        if (materialSetter == null)
            materialSetter = GetComponent<MaterialSetter>();
		materialSetter.SetSingleMaterial(selectedMaterial);
	}

	public bool IsFromSameTeam(Piece piece)
	{
		return team == piece.team;
	}

	public bool CanMoveTo(Vector2Int coords)
	{
		return avaliableMoves.Contains(coords);
	}

	public virtual void MovePiece(Vector2Int coords)
	{

	}


	protected void TryToAddMove(Vector2Int coords)
	{
		avaliableMoves.Add(coords);
	}

	public void SetData(Vector2Int coords, TeamColor team, Board board)
	{
		this.team = team;
		occupiedSquare = coords;
		this.board = board;
		transform.position = board.CalculatePositionFromCoords(coords);
	}

	// public bool IsAttackingPieceOfType<T>() where T : Piece
	// {
	// 	foreach (var square in avaliableMoves)
	// 	{
	// 		if (board.GetPieceOnSquare(square) is T)
	// 			return true;
	// 	}
	// 	return false;
	// }

}
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// // [RequireComponent(typeof(IObjectTweener))]
// [RequireComponent(typeof(MaterialSetter))]
// public abstract class Piece : MonoBehaviour
// {
//     private MaterialSetter materialSetter;
//     public BoardLayout board {protected get; set;}
//     public Vector2Int occupiedSquare {get; set;}
//     public TeamColor team {get; set;}
//     public bool hasMoved {get; private set;}
//     public List<Vector2Int> availableMoves;
//     // private IObjectTweener tweener; 
//     public abstract List<Vector2Int> SelectAvailableSquares();

//     private void Awake()
//     {
//         availableMoves = new List<Vector2Int>();
//         // tweener = GetComponent<IObjectTweener>();
//         materialSetter = GetComponent<MaterialSetter>();
//         hasMoved = false;
//     }

//     public void SetMaterial(Material material)
//     {
//         materialSetter.SetSingleMaterial(material);
//     }

//     public bool IsFromSameTeam(Piece piece)
//     {
//         return team == piece.team;
//     }

//     public bool CanMoveTo(Vector2Int coords)
//     {
//         return availableMoves.Contains(coords);
//     }

//     public virtual void MovePiece(Vector2Int coords)
//     {
//         return; // to be implemented at a future state
//     }

//     protected void TryToAddMove(Vector2Int coords)
//     {
//         availableMoves.Add(coords);
//     }

//     public void SetData(Vector2Int coords, TeamColor team, Board board)
//     {
//         this.team = team;
//         occupiedSquare = coords;
//         this.board = board;
//         transform.position = board.CalculatePositionFromCoords(coords);
//     }







// }