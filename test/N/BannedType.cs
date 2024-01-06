#pragma warning disable CS0067
namespace N
{
    public class BannedType
    {
        public BannedType() {}

        public int BannedMethod() => 0;

        public void BannedMethod(int i) {}

        public void BannedMethod<T>(T t) {}

        public void BannedMethod<T>(Func<T> f) {}

        public string BannedField;

        public string BannedProperty { get; }

        public event EventHandler BannedEvent;

        public static int StaticBannedMethod() => 0;

        public static void StaticBannedMethod(int i) {}

        public static void StaticBannedMethod<T>(T t) {}

        public static void StaticBannedMethod<T>(Func<T> f) {}

        public static string StaticBannedField;

        public static string StaticBannedProperty { get; }

        public static event EventHandler StaticBannedEvent;

    }

    public class BannedType<T>
    {
    }

    public class BannedAttribute : Attribute
    {
    }
}
#pragma warning restore CS0067