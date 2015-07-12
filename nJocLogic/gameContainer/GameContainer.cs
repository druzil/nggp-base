using System.Collections.Generic;
using nJocLogic.data;
using nJocLogic.gdl;
using nJocLogic.knowledge;
using nJocLogic.reasoner.AimaProver;

namespace nJocLogic.gameContainer
{
    public static class GameContainer
    {
        public static SymbolTable SymbolTable { get; private set; }

        public static readonly Parser Parser = new Parser();

        public static GameInformation GameInformation { get; private set; }

        public static AimaProver Prover { get; private set; }

        public static void Initialise(GdlList description)
        {
            SymbolTable = Parser.SymbolTable;

            GameInformation = new MetaGdl(Parser).ExamineGdl(description);

            var expressions = new List<Expression>();
            expressions.AddRange(GameInformation.GetRules());
            expressions.AddRange(GameInformation.GetAllGrounds());
            Prover = new AimaProver(expressions);
        }
    }
}