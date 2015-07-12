using System;
using System.Collections.Generic;
using nJocLogic.gameContainer;

namespace nJocLogic.util.gdl.model
{
    public static class SentenceForms
    {
        public static readonly Predicate<ISentenceForm> TruePred = input => input.Name == GameContainer.Parser.TokTrue;
        public static readonly Predicate<ISentenceForm> DoesPred = input => input.Name == GameContainer.Parser.TokDoes;

        public static HashSet<String> GetNames(ISet<ISentenceForm> forms)
        {
            var names = new HashSet<string>();
            foreach (ISentenceForm form in forms)
                names.Add(GameContainer.SymbolTable[form.Name]);
            return names;
        }
    }
}
