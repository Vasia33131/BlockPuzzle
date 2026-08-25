using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Optional authored collection of figures. When left empty (or not assigned at all)
    /// the library falls back to <see cref="ShapeCatalog"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "ShapeLibrary", menuName = "Block Puzzle/Shape Library")]
    public class ShapeLibrary : ScriptableObject
    {
        [SerializeField] private List<BlockShape> shapes = new List<BlockShape>();

        [Tooltip("Extra figures mixed into the tray after the player buys shapes_pack_1.")]
        [SerializeField] private List<BlockShape> pack1 = new List<BlockShape>();

        [Tooltip("Version of the built-in catalog this asset was generated from.")]
        [SerializeField, HideInInspector] private int catalogVersion;

        private List<BlockShape> runtimeFallback;
        private List<BlockShape> runtimePack1Fallback;

        /// <summary>
        /// Lets the editor generator notice that the built-in catalog has moved on — a
        /// re-skin of the figures would otherwise be invisible behind an asset that was
        /// baked from an older palette.
        /// </summary>
        public int CatalogVersion => catalogVersion;

        public IReadOnlyList<BlockShape> Shapes
        {
            get
            {
                if (shapes != null && shapes.Count > 0)
                {
                    return shapes;
                }

                return runtimeFallback ??= ShapeCatalog.CreateDefaultShapes();
            }
        }

        /// <summary>
        /// Paid pack figures. Empty assets fall back to the built-in pack so a stale
        /// <see cref="ShapeLibrary"/> still delivers the extra set after purchase.
        /// </summary>
        public IReadOnlyList<BlockShape> Pack1
        {
            get
            {
                if (pack1 != null && pack1.Count > 0)
                {
                    return pack1;
                }

                return runtimePack1Fallback ??= ShapeCatalog.CreatePack1Shapes();
            }
        }

        /// <summary>Builds an in-memory library from the built-in catalog.</summary>
        public static ShapeLibrary CreateDefault()
        {
            ShapeLibrary library = CreateInstance<ShapeLibrary>();
            library.name = "Default Shape Library";
            library.shapes = ShapeCatalog.CreateDefaultShapes();
            library.pack1 = ShapeCatalog.CreatePack1Shapes();
            return library;
        }
    }
}
