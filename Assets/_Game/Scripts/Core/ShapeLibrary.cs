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

        [Tooltip("Version of the built-in catalog this asset was generated from.")]
        [SerializeField, HideInInspector] private int catalogVersion;

        private List<BlockShape> runtimeFallback;

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

        /// <summary>Builds an in-memory library from the built-in catalog.</summary>
        public static ShapeLibrary CreateDefault()
        {
            ShapeLibrary library = CreateInstance<ShapeLibrary>();
            library.name = "Default Shape Library";
            library.shapes = ShapeCatalog.CreateDefaultShapes();
            return library;
        }
    }
}
