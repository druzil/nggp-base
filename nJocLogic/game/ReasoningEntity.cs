using System.Collections.Generic;
using System;
using nJocLogic.data;
using nJocLogic.gdl;
using nJocLogic.reasoner.AimaProver;

namespace nJocLogic.game
{
public class ReasoningEntity
{   
    readonly protected Parser              Parser;   
    readonly protected SymbolTable         SymbolTable;    
    protected AimaProver Prover;   
    protected Random                    Random;
    
    readonly protected Fact QueryTerminal;
    readonly protected Fact QueryNext;

    protected ReasoningEntity(Parser parser)
    {
        Parser = parser;
        SymbolTable = Parser.SymbolTable;
        
        QueryTerminal = MakeQuery("terminal");
        QueryNext = MakeQuery("next", "?x");
        
        Random = new Random();
    }   
    
    protected Fact MakeQuery(params String[] args)
    {
        GdlList list = GdlList.BuildFromWords(SymbolTable, args);
        Fact query = VariableFact.FromList(list);
            
        return query;
    }
    
    /**
     * Wrapper around Reasoner#getAllAnswers
     * 
     * @param context
     *            The context to be used in the proof. Contains volatile data,
     *            cache, etc.
     * @param args
     *            The question (query) as a list of words
     * 
     * @return a list of facts answering the input query
     * @see camembert.knowledge.reasoner.getAllAnswers
     */
    //protected IEnumerable<GroundFact> GetAllAnswers(ProofContext context, params String[] args)
    //{
    //    Fact question = MakeQuery(args);
    //    return Reasoner.GetAllAnswers(question, context);
    //}

    protected HashSet<Fact> GetAllAnswers(HashSet<Fact> context, params String[] args)
    {
        Fact question = MakeQuery(args);
        return Prover.AskAll(question, context);
    }    

    /**
     * Wrapper around Reasoner#getAnAnswer
     * 
     * @param context
     *            The context to be used in the proof. Contains volatile data,
     *            cache, etc.
     * @param args
     *            The question (query) as a list of words
     *            
     * @return a fact answering the input query
     * @see camembert.knowledge.reasoner.getAnAnswer
     */
    //protected GroundFact GetAnAnswer(ProofContext context, params String[] args)
    //{
    //    Fact question = MakeQuery(args);
    //    return Reasoner.GetAnAnswer(question, context);
    //}

    protected Fact GetAnAnswer(HashSet<Fact> context, params String[] args)
    {
        Fact question = MakeQuery(args);
        return Prover.AskOne(question, context);
    }
    
}

}
