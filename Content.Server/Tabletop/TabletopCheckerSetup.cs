using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Tabletop
{
    [UsedImplicitly]
    public sealed partial class TabletopCheckerSetup : TabletopSetup
    {

        [DataField("prototypePieceWhite", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
        public string PrototypePieceWhite = default!;

        [DataField("prototypeCrownWhite", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
        public string PrototypeCrownWhite = default!;

        [DataField("prototypePieceBlack", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
        public string PrototypePieceBlack = default!;

        [DataField("prototypeCrownBlack", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
        public string PrototypeCrownBlack = default!;

        public override void SetupTabletop(TabletopSession session, IEntityManager entityManager)
        {
            SpawnPiece(entityManager, session, BoardPrototype, session.Position.Offset(-1, 0));
            SpawnPieces(session, entityManager, session.Position.Offset(-4.5f, 3.5f));
        }

        private void SpawnPieces(TabletopSession session, IEntityManager entityManager, MapCoordinates left)
        {
            static float GetOffset(float offset) => offset * 1f /* separation */;

            // Pieces
            for (var offsetY = 0; offsetY < 3; offsetY++)
            {
                var checker = offsetY % 2;

                for (var offsetX = 0; offsetX < 8; offsetX += 2)
                {
                    // Prevents an extra piece on the middle row
                    if (checker + offsetX > 8) continue;

                    SpawnPiece(entityManager, session,
                        PrototypePieceBlack,
                        left.Offset(GetOffset(offsetX + (1 - checker)), GetOffset(offsetY * -1))
                    );
                    SpawnPiece(entityManager, session,
                        PrototypePieceWhite,
                        left.Offset(GetOffset(offsetX + checker), GetOffset(offsetY - 7))
                    );
                }
            }

            const int NumCrowns = 3;
            const float Overlap = 0.25f;
            const float xOffset = 9f / 32;
            const float xOffsetBlack = 9 + xOffset;
            const float xOffsetWhite = 8 + xOffset;

            // Crowns
            for (var i = 0; i < NumCrowns; i++)
            {
                var step = -(Overlap * i);
                SpawnPiece(entityManager, session,
                    PrototypeCrownBlack,
                    left.Offset(GetOffset(xOffsetBlack), GetOffset(step))
                );
                SpawnPiece(entityManager, session,
                    PrototypeCrownWhite,
                    left.Offset(GetOffset(xOffsetWhite), GetOffset(step))
                );
            }

            // Spares
            for (var i = 0; i < 6; i++)
            {
                var step = -((Overlap * (NumCrowns + 2)) + (Overlap * i));
                SpawnPiece(entityManager, session,
                    PrototypePieceBlack,
                    left.Offset(GetOffset(xOffsetBlack), GetOffset(step))
                );
                SpawnPiece(entityManager, session,
                    PrototypePieceWhite,
                    left.Offset(GetOffset(xOffsetWhite), GetOffset(step))
                );
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
