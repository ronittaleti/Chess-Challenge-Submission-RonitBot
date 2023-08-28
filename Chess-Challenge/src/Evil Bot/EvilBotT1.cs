﻿//#define DEBUG_TIMER
using ChessChallenge.API;
using System;
using System.Linq;

public class EvilBotT1 : IChessBot
{
    //                     .  P    K    B    R    Q    K
    int[] kPieceValues = { 0, 100, 300, 310, 500, 900, 10000 };
    int kMassiveNum = 99999999;

#if DEBUG_TIMER
	int dNumMovesMade = 0;
	int dTotalMsElapsed = 0;
#endif

    int mDepth;
    Move mBestMove;

    public Move Think(Board board, Timer timer)
    {
        Move[] legalMoves = board.GetLegalMoves();
        mDepth = 5;

        EvaluateBoardNegaMax(board, mDepth, -kMassiveNum, kMassiveNum, board.IsWhiteToMove ? 1 : -1);

#if DEBUG_TIMER
		dNumMovesMade++;
		dTotalMsElapsed += timer.MillisecondsElapsedThisTurn;
		Console.WriteLine("My bot time average: {0}", (float)dTotalMsElapsed / dNumMovesMade);
#endif
        return mBestMove;
    }

    int EvaluateBoardNegaMax(Board board, int depth, int alpha, int beta, int color)
    {
        Move[] legalMoves;

        if (board.IsDraw())
            return 0;

        if (depth == 0 || (legalMoves = board.GetLegalMoves()).Length == 0)
        {
            // EVALUATE
            int sum = 0;

            if (board.IsInCheckmate())
                return board.IsWhiteToMove ? -kMassiveNum : kMassiveNum;

            for (int i = 0; ++i < 7;)
                sum += (board.GetPieceList((PieceType)i, true).Count - board.GetPieceList((PieceType)i, false).Count) * kPieceValues[i];
            // EVALUATE

            return color * sum;
        }

        // TREE SEARCH
        int recordEval = -kMassiveNum;
        foreach (Move move in legalMoves)
        {
            board.MakeMove(move);
            int evaluation = -EvaluateBoardNegaMax(board, depth - 1, -beta, -alpha, -color);
            board.UndoMove(move);

            if (recordEval < evaluation)
            {
                recordEval = evaluation;
                if (depth == mDepth) mBestMove = move;
            }
            alpha = Math.Max(alpha, recordEval);
            if (alpha >= beta) break;
        }
        // TREE SEARCH

        return recordEval;
    }
}
