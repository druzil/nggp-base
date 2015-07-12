using nJocLogic.data;

namespace nJocLogic.reasoner.AimaProver
{
    public class Unifier
    {
        public static Substitution Unify(Fact x, Fact y)
        {
            Substitution theta = new Substitution();
            bool isGood = UnifyTerm(x.ToTerm(), y.ToTerm(), theta);

            return isGood ? theta : null;
        }

        private static bool UnifyTerm(Term x, Term y, Substitution theta)
        {
            if (x.Equals(y))
                return true;

            if (x is TermVariable)
            {
                if (!UnifyVariable((TermVariable)x, y, theta))
                    return false;
            }
            else if (y is TermVariable)
            {
                if (!UnifyVariable((TermVariable)y, x, theta))
                    return false;
            }
            else if ((x is TermFunction) && (y is TermFunction))
            {
                var xFunction = (TermFunction)x;
                var yFunction = (TermFunction)y;

                if (xFunction.FunctionName != yFunction.FunctionName)
                    return false;

                for (int i = 0; i < xFunction.Arity; i++)
                {
                    if (!UnifyTerm(xFunction.GetTerm(i), yFunction.GetTerm(i), theta))
                        return false;
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        private static bool UnifyVariable(TermVariable var, Term x, Substitution theta)
        {
            if (theta.Contains(var))
                return UnifyTerm(theta[var], x, theta);

            TermVariable termVariable = x as TermVariable;
            if ((termVariable != null) && theta.Contains(termVariable))
                return UnifyTerm(var, theta[termVariable], theta);

            theta[var] = x;
            return true;
        }
    }

}