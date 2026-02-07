using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Tabletop
{
    [UsedImplicitly]
    public sealed partial class TabletopChessSetup : TabletopSetup
    {

        // TODO: Un-hardcode the rest of entity prototype IDs, probably.

        public override void SetupTabletop(TabletopSession session, IEntityManager entityManager)
        {
            SpawnPiece(entityManager, session, BoardPrototype, session.Position.Offset(-1, 0));
            SpawnPieces(session, entityManager, session.Position.Offset(-4.5f, 3.5f));
        }

        private void SpawnPieces(TabletopSession session, IEntityManager entityManager, MapCoordinates topLeft, float separation = 1f)
        {
            var (mapId, x, y) = topLeft;

            // Spawn all black pieces
            SpawnPiecesRow(session, entityManager, "Black", topLeft, separation);
            SpawnPawns(session, entityManager, "Black", new MapCoordinates(x, y - separation, mapId) , separation);

            // Spawn all white pieces
            SpawnPawns(session, entityManager, "White", new MapCoordinates(x, y - 6 * separation, mapId) , separation);
            SpawnPiecesRow(session, entityManager, "White", new MapCoordinates(x, y - 7 * separation, mapId), separation);

            // Extra queens
            SpawnPiece(entityManager, session, "BlackQueen", new MapCoordinates(x + 9 * separation + 9f / 32, y - 3 * separation, mapId));
            SpawnPiece(entityManager, session, "WhiteQueen", new MapCoordinates(x + 9 * separation + 9f / 32, y - 4 * separation, mapId));
        }

        // TODO: refactor to load FEN instead
        private void SpawnPiecesRow(TabletopSession session, IEntityManager entityManager, string color, MapCoordinates left, float separation = 1f)
        {
            const string piecesRow = "rnbqkbnr";

            var (mapId, x, y) = left;

            for (int i = 0; i < 8; i++)
            {
                switch (piecesRow[i])
                {
                    case 'r':
                        SpawnPiece(entityManager, session, color + "Rook", new MapCoordinates(x + i * separation, y, mapId));
                        break;
                    case 'n':
                        SpawnPiece(entityManager, session, color + "Knight", new MapCoordinates(x + i * separation, y, mapId));
                        break;
                    case 'b':
                        SpawnPiece(entityManager, session, color + "Bishop", new MapCoordinates(x + i * separation, y, mapId));
                        break;
                    case 'q':
                        SpawnPiece(entityManager, session, color + "Queen", new MapCoordinates(x + i * separation, y, mapId));
                        break;
                    case 'k':
                        SpawnPiece(entityManager, session, color + "King", new MapCoordinates(x + i * separation, y, mapId));
                        break;
                }
            }
        }

        // TODO: refactor to load FEN instead
        private void SpawnPawns(TabletopSession session, IEntityManager entityManager, string color, MapCoordinates left, float separation = 1f)
        {
            var (mapId, x, y) = left;

            for (int i = 0; i < 8; i++)
            {
                SpawnPiece(entityManager, session, color + "Pawn", new MapCoordinates(x + i * separation, y, mapId));
            }
        }

        private EntityUid SpawnPiece(IEntityManager entityManager, TabletopSession session, string proto, MapCoordinates coords)
        {
            var uid = entityManager.SpawnEntity(proto, coords);
            entityManager.GetComponent<TransformComponent>(uid).LocalRotation = Angle.Zero;
            session.Entities.Add(uid);
            return uid;
        }
    }
}
