namespace Utilities.Utilities
{
    public class ValueHolder<T>
    {
        private T Value { get; set; }

        public ValueHolder()
        {
            Value = default;
        }

        public T Get() => Value;

        public void Set(T value) => Value = value;
    }
}
