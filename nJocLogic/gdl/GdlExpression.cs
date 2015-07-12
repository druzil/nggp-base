namespace nJocLogic.gdl
{
    public abstract class GdlExpression
    {
        protected readonly SymbolTable symbolTable_;

        protected GdlExpression(SymbolTable symTab)
        {
            symbolTable_ = symTab;
        }
    }
}
