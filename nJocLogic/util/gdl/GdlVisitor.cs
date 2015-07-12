using System;
using nJocLogic.data;

namespace nJocLogic.util.gdl
{
    /// <summary>
    /// A visitor for Expression objects. The GdlVisitors class has methods for going
    /// through a Expression object or collection thereof and applying the visitor methods
    /// to all relevant Expression objects.
    /// 
    /// This visitor uses the adapter design pattern, providing empty implementations
    /// of each method so subclasses need only implement the relevant methods.
    /// </summary>
    public class GdlVisitor
    {
        public Action<Expression> VisitGdl = gdl => { };
        public Action<Term> VisitTerm = term => { };
        public Action<TermObject> VisitConstant = x => { };
        public Action<TermVariable> VisitVariable = x => { };
        public Action<TermFunction> VisitFunction = x => { };
        public Action<Fact> VisitSentence = x => { };
        public Action<Fact> VisitRelation = x => { };

        public Action<Negation> VisitNot = x => { };
        public Action<Disjunction> VisitOr = x => { };
        public Action<Implication> VisitRule = x => { };
        public Action<Expression> VisitLiteral = x => { };
        public Action<Fact> VisitProposition = x => { };
        public Action<Fact> VisitDistinct = x => { };
    }
}
