using System;

namespace IDE_COMPILADOR.AnalizadorSemantico
{
    public enum DataType
    {
        Unknown = 0,
        Int,
        Float,
        Bool
    }

    public static class TypeUtils
    {
        public static DataType FromString(string s)
        {
            switch (s)
            {
                case "int": return DataType.Int;
                case "float": return DataType.Float;
                case "bool": return DataType.Bool;
                default: return DataType.Unknown;
            }
        }

        public static string ToSource(this DataType t)
        {
            switch (t)
            {
                case DataType.Int: return "int";
                case DataType.Float: return "float";
                case DataType.Bool: return "bool";
                default: return "unknown";
            }
        }

        public static bool IsNumeric(this DataType t) => t == DataType.Int || t == DataType.Float;

        public static int SizeOf(this DataType t)
        {
            switch (t)
            {
                case DataType.Int: return 4;
                case DataType.Float: return 8;
                case DataType.Bool: return 1;
                default: return 0;
            }
        }

        /// <summary>
        /// Reglas de asignación:
        /// - Igual tipo: OK
        /// - int → float: OK (promoción)
        /// - float → int: ERROR (angosta)
        /// - bool solo con bool
        /// </summary>
        public static bool CanAssign(DataType target, DataType source, out bool isNarrowing)
        {
            isNarrowing = false;

            if (target == source) return true;

            if (target == DataType.Float && source == DataType.Int) return true;

            if (target == DataType.Int && source == DataType.Float)
            {
                isNarrowing = true;
                return false;
            }

            if (target == DataType.Bool || source == DataType.Bool)
                return false;

            return false;
        }

        public static DataType PromoteNumeric(DataType a, DataType b)
        {
            if (a == DataType.Float || b == DataType.Float) return DataType.Float;
            if (a == DataType.Int && b == DataType.Int) return DataType.Int;
            return DataType.Unknown;
        }
    }
}
