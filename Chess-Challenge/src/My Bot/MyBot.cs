using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{

    private readonly int[]
        pieceValues = { 0, 100, 300, 300, 500, 900, 10000 }, // None, Pawn, Knight, Bishop, Rook, Queen, King 
        PieceMiddlegameValues = { 82, 337, 365, 477, 1025, 0 },
        PieceEndgameValues = { 94, 281, 297, 512, 936, 0 },
        GamePhaseIncrement = { 0, 1, 1, 2, 4, 0 };

    Move[] killerMoves = new Move[4096]; // Max depth for killer move storage

    Move bestMove;

    (ulong, Move, int, int, int)[] TTtable = new (ulong, Move, int, int, int)[1048576];

    int[,,] history;

    // Pawn, Knight, Bishop, Rook, Queen, King 
    private readonly short[] _pieceValues =
    {
        82, 337, 365, 477, 1025, 20000,
        94, 281, 297, 512, 936, 20000
    };

    private readonly decimal[] _packedPst =
    {
        63746705523041458768562654720m, 71818693703096985528394040064m, 75532537544690978830456252672m,
        75536154932036771593352371712m, 76774085526445040292133284352m, 3110608541636285947269332480m,
        936945638387574698250991104m, 75531285965747665584902616832m, 77047302762000299964198997571m,
        3730792265775293618620982364m, 3121489077029470166123295018m, 3747712412930601838683035969m,
        3763381335243474116535455791m, 8067176012614548496052660822m, 4977175895537975520060507415m,
        2475894077091727551177487608m, 2458978764687427073924784380m, 3718684080556872886692423941m,
        4959037324412353051075877138m, 3135972447545098299460234261m, 4371494653131335197311645996m,
        9624249097030609585804826662m, 9301461106541282841985626641m, 2793818196182115168911564530m,
        77683174186957799541255830262m, 4660418590176711545920359433m, 4971145620211324499469864196m,
        5608211711321183125202150414m, 5617883191736004891949734160m, 7150801075091790966455611144m,
        5619082524459738931006868492m, 649197923531967450704711664m, 75809334407291469990832437230m,
        78322691297526401047122740223m, 4348529951871323093202439165m, 4990460191572192980035045640m,
        5597312470813537077508379404m, 4980755617409140165251173636m, 1890741055734852330174483975m,
        76772801025035254361275759599m, 75502243563200070682362835182m, 78896921543467230670583692029m,
        2489164206166677455700101373m, 4338830174078735659125311481m, 4960199192571758553533648130m,
        3420013420025511569771334658m, 1557077491473974933188251927m, 77376040767919248347203368440m,
        73949978050619586491881614568m, 77043619187199676893167803647m, 1212557245150259869494540530m,
        3081561358716686153294085872m, 3392217589357453836837847030m, 1219782446916489227407330320m,
        78580145051212187267589731866m, 75798434925965430405537592305m, 68369566912511282590874449920m,
        72396532057599326246617936384m, 75186737388538008131054524416m, 77027917484951889231108827392m,
        73655004947793353634062267392m, 76417372019396591550492896512m, 74568981255592060493492515584m,
        70529879645288096380279255040m,
    };

    private readonly int[][] _pst;

    public MyBot()
    {
        _pst = _packedPst.Select(packedTable =>
        {
            int pieceType = 0;
            return decimal.GetBits(packedTable).Take(3)
                .SelectMany(bit => BitConverter.GetBytes(bit)
                    .Select(square => (int)((sbyte)square * 1.461) + _pieceValues[pieceType++]))
                .ToArray();
        }).ToArray();
    }

    public Move Think(Board board, Timer timer)
    {
        bestMove = Move.NullMove;
        history = new int[2, 7, 64];
        for (int ply = 0; ++ply < 51;)
        {
            killerMoves[ply] = Move.NullMove;
        }
        for (int depth = 1; ++depth <= 50;)
        {
            Search(board, timer, depth, -10000000, 10000000, 0);

            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30) break;
        }
        return bestMove.IsNull ? board.GetLegalMoves()[0] : bestMove;
    }

    public int Search(Board board, Timer timer, int depth, int alpha, int beta, int ply)
    {
        bool inCheck = board.IsInCheck();
        int maxScore = int.MinValue, oldAlpha = alpha;
        bool qsearch = depth <= 0;

        if (!qsearch && board.GetLegalMoves().Length == 0)
            return inCheck ? ply - 10000000 : 0;

        if (ply > 0 && board.IsRepeatedPosition())
            return 0;

        var (ttKey, ttMove, ttDepth, ttScore, ttBound) = TTtable[board.ZobristKey % 1048576];

        if (qsearch)
        {
            maxScore = Evaluate(board, ttKey, ttScore);
            if (maxScore >= beta)
                return maxScore;
            alpha = Math.Max(alpha, maxScore);
        }

        if (ttKey == board.ZobristKey && ttDepth >= depth && ply > 0 && (ttBound == 1 || (ttBound == 2 && ttScore >= beta) || (ttBound == 3 && ttScore <= alpha)))
            return ttScore;

        Move[] legalMoves = board.GetLegalMoves(qsearch);
        int[] moveScores = new int[legalMoves.Length];
        for (int i = 0; i < moveScores.Length; i++)
        {
            Move move = legalMoves[i];
            moveScores[i] = -(
                ttMove == move ? int.MaxValue :
                move.IsCapture ? 1000000 * (int)move.CapturePieceType - (int)move.MovePieceType :
                move.IsPromotion ? 1000000 :
                killerMoves[ply] == move ? 900000 :
                history[ply & 1, (int)move.MovePieceType, move.TargetSquare.Index]);
        }
        Array.Sort(moveScores, legalMoves);

        if (inCheck) depth++;

        int numMoves = 0;

        foreach (Move move in legalMoves)
        {
            board.MakeMove(move);
            bool firstNode = numMoves == 0;
            int doLmr = depth >= 3 && !(move.IsCapture || board.IsInCheck() || !firstNode) ? 1 : 0;

            int score = -Search(board, timer, depth - 1 - doLmr, firstNode ? -beta : -alpha - 1, -alpha, ply + 1);
            score = alpha < score && (score < beta || doLmr == 1) && !firstNode ? -Search(board, timer, depth - 1, -beta, -alpha, ply + 1) : score;
            board.UndoMove(move);

            if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30)
                return 10000000;

            if (score > maxScore)
            {
                maxScore = score;
                if (ply == 0)
                    bestMove = move;
                alpha = Math.Max(alpha, maxScore);
                if (alpha >= beta)
                {
                    if (!move.IsCapture && !move.IsPromotion)
                    {
                        if (move != killerMoves[ply] && ply < 51)
                        {
                            killerMoves[ply] = move;
                        }
                        history[ply & 1, (int)move.MovePieceType, move.TargetSquare.Index] += depth * depth;
                    }
                    break;
                }
            }

            numMoves++;
        }

        TTtable[board.ZobristKey % 1048576] = (board.ZobristKey, bestMove, depth, maxScore, maxScore < oldAlpha ? 3 : maxScore >= beta ? 2 : 1);

        return maxScore;
    }

    private int Evaluate(Board board, ulong ttKey, int ttScore)
    {
        if (ttKey == board.ZobristKey) return ttScore;
        int sum = 0;
        for (int i = 0; ++i < 7;)
            sum += (board.GetPieceList((PieceType)i, true).Count - board.GetPieceList((PieceType)i, false).Count) * pieceValues[i];

        int middlegame = 0, endgame = 0, gamephase = 0, sideToMove = 2, piece, square;
        for (; --sideToMove >= 0; middlegame = -middlegame, endgame = -endgame)
            for (piece = -1; ++piece < 6;)
                for (ulong mask = board.GetPieceBitboard((PieceType)piece + 1, sideToMove > 0); mask != 0;)
                {
                    // Gamephase, middlegame -> endgame
                    gamephase += GamePhaseIncrement[piece];

                    // Material and square evaluation
                    square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ 56 * sideToMove;
                    middlegame += _pst[square][piece];
                    endgame += _pst[square][piece + 6];
                }

        sum += (middlegame * gamephase + endgame * (24 - gamephase)) / 24;

        return sum * (board.IsWhiteToMove ? 1 : -1);
    }
}