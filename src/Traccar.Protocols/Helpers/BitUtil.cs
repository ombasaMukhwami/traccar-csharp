namespace Traccar.Protocols.Helpers;

public static class BitUtil
{
    public static bool Check(long number, int index) => (number & (1L << index)) != 0;

    public static int Between(int number, int from, int to) => (number >> from) & ((1 << (to - from)) - 1);

    public static int From(int number, int from) => number >> from;

    public static int To(int number, int to) => Between(number, 0, to);

    public static long Between(long number, int from, int to) => (number >> from) & ((1L << (to - from)) - 1L);

    public static long From(long number, int from) => number >> from;

    public static long To(long number, int to) => Between(number, 0, to);
}
