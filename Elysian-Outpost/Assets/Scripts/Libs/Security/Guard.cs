namespace Libs.Security
{
    public static class Guard
    {
        public static void IsNotNull<T>(T value, string paramName) where T : class
        {
            if (value == null)
            {
                throw new System.ArgumentNullException(paramName);
            }
        }

        public static void IsGreaterThan(int value, int minimum, string paramName)
        {
            if (value <= minimum)
            {
                throw new System.ArgumentOutOfRangeException(paramName, $"Value must be greater than {minimum}");
            }
        }

        public static void IsInRange(int value, int minimum, int maximum, string paramName)
        {
            if (value < minimum || value >= maximum)
            {
                throw new System.ArgumentOutOfRangeException(paramName, $"Value must be between {minimum} (inclusive) and {maximum} (exclusive)");
            }
        }
    }
}