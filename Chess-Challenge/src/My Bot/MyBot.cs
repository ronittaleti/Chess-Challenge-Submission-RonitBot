using System;
using ChessChallenge.API;
using Raylib_cs;

public class MyBot : IChessBot
{
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };
    Move bestMove;

    int checkMateEval = int.MaxValue;

    public Move Think(Board board, Timer timer)
    {
        bestMove = Move.NullMove;
        Search(board, timer, 6, -checkMateEval, checkMateEval, 0);
        return bestMove.IsNull ? board.GetLegalMoves()[0] : bestMove;
    }

    public int QuiesceSearch(Board board, Timer timer, int alpha, int beta)
    {
        int stand_pat = Evaluate(board);

        if (stand_pat >= beta) return stand_pat;

        if (alpha < stand_pat) alpha = stand_pat;

        Move[] legalCaptureMoves = board.GetLegalMoves(true);

        foreach (Move move in legalCaptureMoves)
        {
            board.MakeMove(move);
            int score = -QuiesceSearch(board, timer, -beta, -alpha);
            board.UndoMove(move);

            if (score >= beta) return beta;

            if (score > alpha) alpha = score;
        }

        return alpha;
    }

    public int Search(Board board, Timer timer, int depth, int alpha, int beta, int ply)
    {
        if (ply > 0 && board.IsRepeatedPosition()) {
            return 0;
        }

        if (depth == 0)
        {
            return QuiesceSearch(board, timer, alpha, beta);
        }

        int maxScore = int.MinValue;

        Move[] legalMoves = board.GetLegalMoves();

        foreach (Move move in legalMoves)
        {
            if (MoveIsCheckmate(board, move))
            {
                if (ply % 2 == 0 && ply != 0)
                {
                    maxScore = 999999-ply;
                    break;
                }
                //if (ply % 2 == 1)
                //{
                //    maxScore = -999999+ply;
                //    break;
                //}
                if (ply == 0)
                {
                    bestMove = move;
                    break;
                }
            }
            board.MakeMove(move);
            int score = -Search(board, timer, depth - 1, -beta, -alpha, ply + 1);
            board.UndoMove(move);
            if (score > maxScore)
            {
                maxScore = score;
                if (ply == 0) {
                    bestMove = move;
                }
            }
            if (maxScore > alpha)
            {
                alpha = maxScore;
            }
            if (alpha >= beta)
            {
                break;
            }
        }

        return maxScore;
    }

    private int Evaluate(Board board)
    {
        int sum = 0;
        for (int i = 0; ++i < 7;)
            sum += (board.GetPieceList((PieceType)i, true).Count - board.GetPieceList((PieceType)i, false).Count) * pieceValues[i];

        return sum * (board.IsWhiteToMove ? 1 : -1);
    }

    bool MoveIsCheckmate(Board board, Move move)
    {
        board.MakeMove(move);
        bool isMate = board.IsInCheckmate();
        board.UndoMove(move);
        return isMate;
    }
}