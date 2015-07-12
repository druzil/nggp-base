using System.Text;
namespace nJocLogic.util
{
    public class Pair<Type1, Type2>
    {
        public Type1 first;
        public Type2 second;

        public Pair(Type1 a, Type2 b)
        {
            first = a;
            second = b;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append('<');
            sb.Append(first);
            sb.Append(';');
            sb.Append(second);
            sb.Append('>');

            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Pair<Type1, Type2>))
                return false;
            Pair<Type1, Type2> pair = (Pair<Type1, Type2>)obj;
            return first.Equals(pair.first) && second.Equals(pair.second);
        }

        public override int GetHashCode()
        {
            return first.GetHashCode() + second.GetHashCode();
        }
    }

}
