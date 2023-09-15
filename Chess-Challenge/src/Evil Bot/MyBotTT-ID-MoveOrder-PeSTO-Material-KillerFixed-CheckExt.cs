using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;
using Raylib_cs;

public class MyBotTTIDMoveOrderPeSTOMaterialKillerFixedCheckExt : IChessBot
{
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };

    // None, Pawn, Knight, Bishop, Rook, Queen, King
    private readonly int[] PieceMiddlegameValues = { 82, 337, 365, 477, 1025, 0 };
    private readonly int[] PieceEndgameValues = { 94, 281, 297, 512, 936, 0 };

    private readonly int[] GamePhaseIncrement = { 0, 1, 1, 2, 4, 0 };

    static private int MAX_PLY = 5;
    Move[,] killerMoves = new Move[MAX_PLY, 2]; // Max depth for killer move storage

    (ulong, Move, int, int, int)[] TTtable = new (ulong, Move, int, int, int)[1048576];

    // Big table packed with data from premade piece square tables
    // Unpack using PackedEvaluationTables[set, rank] = file
    private readonly ulong[] PackedEvaluationTables = {
        0, 17876852006827220035, 17442764802556560892, 17297209133870877174, 17223739749638733806, 17876759457677835758, 17373217165325565928, 0,
        13255991644549399438, 17583506568768513230, 2175898572549597664, 1084293395314969850, 18090411128601117687, 17658908863988562672, 17579252489121225964, 17362482624594506424,
        18088114097928799212, 16144322839035775982, 18381760841018841589, 18376121450291332093, 218152002130610684, 507800692313426432, 78546933140621827, 17502669270662184681,
        2095587983952846102, 2166845185183979026, 804489620259737085, 17508614433633859824, 17295224476492426983, 16860632592644698081, 14986863555502077410, 17214733645651245043,
        2241981346783428845, 2671522937214723568, 2819295234159408375, 143848006581874414, 18303471111439576826, 218989722313687542, 143563254730914792, 16063196335921886463,
        649056947958124756, 17070610696300068628, 17370107729330376954, 16714810863637820148, 15990561411808821214, 17219209584983537398, 362247178929505537, 725340149412010486,
        0, 9255278100611888762, 4123085205260616768, 868073221978132502, 18375526489308136969, 18158510399056250115, 18086737617269097737, 0,
        13607044546246993624, 15920488544069483503, 16497805833213047536, 17583469180908143348, 17582910611854720244, 17434276413707386608, 16352837428273869539, 15338966700937764332,
        17362778423591236342, 17797653976892964347, 216178279655209729, 72628283623606014, 18085900871841415932, 17796820590280441592, 17219225120384218358, 17653536572713270000,
        217588987618658057, 145525853039167752, 18374121343630509317, 143834816923107843, 17941211704168088322, 17725034519661969661, 18372710631523548412, 17439054852385800698,
        1010791012631515130, 5929838478495476, 436031265213646066, 1812447229878734594, 1160546708477514740, 218156326927920885, 16926762663678832881, 16497506761183456745,
        17582909434562406605, 580992990974708984, 656996740801498119, 149207104036540411, 17871989841031265780, 18015818047948390131, 17653269455998023918, 16424899342964550108,
    };

    private int GetSquareBonus(int type, bool isWhite, int file, int rank)
    {
        // Mirror vertically for white pieces, since piece arrays are flipped vertically
        if (isWhite)
            rank = 7 - rank;

        // Grab the correct byte representing the value
        // And multiply it by the reduction factor to get our original value again
        return (int)Math.Round(unchecked((sbyte)((PackedEvaluationTables[(type * 8) + rank] >> file * 8) & 0xFF)) * 1.461);
    }

    Move bestMove;

    public Move Think(Board board, Timer timer)
    {
        bestMove = Move.NullMove;
        for (int ply = 0; ply < MAX_PLY; ply++)
        {
            killerMoves[ply, 0] = Move.NullMove;
            killerMoves[ply, 1] = Move.NullMove;
        }
        for (int depth = 1; depth <= 50; depth++)
        {
            Search(board, timer, depth, -10000000, 10000000, 0, 0);

            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30) break;
        }
        return bestMove.IsNull ? board.GetLegalMoves()[0] : bestMove;
    }

    public int Search(Board board, Timer timer, int depth, int alpha, int beta, int ply, int numExtensions)
    {
        if (ply > 0 && board.IsRepeatedPosition()) return 0;

        int oldAlpha = alpha;

        if (ply > 0 && board.IsRepeatedPosition())
        {
            return 0;
        }

        var (ttKey, ttMove, ttDepth, ttScore, ttBound) = TTtable[board.ZobristKey % 1048576];

        bool qsearch = depth <= 0;

        int maxScore = int.MinValue;

        if (qsearch)
        {
            maxScore = Evaluate(board);
            if (maxScore >= beta) return maxScore;
            alpha = Math.Max(alpha, maxScore);
        }

        if (ttKey == board.ZobristKey && ttDepth >= depth && ply > 0)
        {
            //If we have an "exact" score (a <    score < beta) just use that
            if (ttBound == 1) return ttScore;
            //If we have a lower bound better than beta, use that
            if (ttBound == 2 && ttScore >= beta) return ttScore;
            //If we have an upper bound worse than alpha, use that
            if (ttBound == 3 && ttScore <= alpha) return ttScore;
        }

        Move[] legalMoves = board.GetLegalMoves(qsearch).OrderByDescending(move =>
            ttMove == move ? 500000 :
            move.IsCapture ? 10 * (int)move.CapturePieceType - (int)move.MovePieceType :
            move.IsPromotion ? 10 :
            killerMoves[Math.Min(ply, MAX_PLY - 1), 0] == move || killerMoves[Math.Min(ply, MAX_PLY - 1), 1] == move ? 9 :
            0
        ).ToArray();

        if (board.GetLegalMoves().Length == 0)
        {
            if (board.IsInCheck()) return ply - 10000000; else return 0;
        }

        foreach (Move move in legalMoves)
        {
            board.MakeMove(move);
            int extension = numExtensions < 16 && board.IsInCheck() ? 1 : 0;
            int score = -Search(board, timer, depth - 1 + extension, -beta, -alpha, ply + 1, numExtensions + extension);
            board.UndoMove(move);

            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30) return 10000000;

            if (score > maxScore)
            {
                maxScore = score;
                if (ply == 0)
                {
                    bestMove = move;
                }
            }
            alpha = Math.Max(alpha, maxScore);
            if (alpha >= beta)
            {
                if (!move.IsCapture && !move.IsPromotion && ply < MAX_PLY && move != killerMoves[ply, 0])
                {
                    killerMoves[ply, 1] = killerMoves[ply, 0];
                    killerMoves[ply, 0] = move;
                }
                break;
            }
        }

        TTtable[board.ZobristKey % 1048576] = (board.ZobristKey, bestMove, depth, maxScore, oldAlpha <= alpha ? 3 : maxScore >= beta ? 2 : 1);

        return maxScore;
    }

    private int Evaluate(Board board)
    {
        int sum = 0;
        for (int i = 0; ++i < 7;)
            sum += (board.GetPieceList((PieceType)i, true).Count - board.GetPieceList((PieceType)i, false).Count) * pieceValues[i];

        var middlegame = new int[2];
        var endgame = new int[2];

        int gamephase = 0;

        // TODO: Initialize tables with piece values beforehand to see if tokens can be saved
        foreach (PieceList list in board.GetAllPieceLists())
        {
            int pieceType = (int)list.TypeOfPieceInList - 1;
            int colour = list.IsWhitePieceList ? 1 : 0;

            // Material evaluation
            middlegame[colour] += PieceMiddlegameValues[pieceType] * list.Count;
            endgame[colour] += PieceEndgameValues[pieceType] * list.Count;

            // Square evaluation
            foreach (Piece piece in list)
            {
                middlegame[colour] += GetSquareBonus(pieceType, piece.IsWhite, piece.Square.File, piece.Square.Rank);
                endgame[colour] += GetSquareBonus(pieceType + 6, piece.IsWhite, piece.Square.File, piece.Square.Rank);
            }
            gamephase += GamePhaseIncrement[pieceType];
        }

        // Tapered evaluation
        int middlegameScore = middlegame[1] - middlegame[0];
        int endgameScore = endgame[1] - endgame[0];
        int middlegamePhase = Math.Min(gamephase, 24);
        int endgamePhase = 24 - middlegamePhase;

        sum += (middlegameScore * middlegamePhase + endgameScore * endgamePhase) / 24;

        return sum * (board.IsWhiteToMove ? 1 : -1);
    }

}