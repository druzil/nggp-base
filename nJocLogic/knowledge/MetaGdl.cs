using System.Collections.Generic;
using NLog;
using C5;
using nJocLogic.data;
using nJocLogic.gdl;
using System;
using System.IO;
using nJocLogic.util.gdl.GdlCleaner;

namespace nJocLogic.knowledge
{
    public class MetaGdl
    {
        private readonly TreeDictionary<int, RelationInfo> _relations;
        private readonly TreeSet<int> _functionSymbols;
        private readonly TreeSet<int> _objectSymbols;

        /** The parser that was used to create these symbols. */
        private readonly Parser _parser;

        /** Rules that were extracted during GDL examination. */
        private readonly TreeDictionary<int, List<Implication>> _rules;

        /** Ground facts that were extracted during GDL examination. */
        private readonly TreeDictionary<int, List<GroundFact>> _groundFacts;

        /** Ground facts corresponding to static relations */
        private TreeDictionary<int, List<GroundFact>> _staticRelations;


        /** Ordered list of roles in the game. */
        private readonly List<TermObject> _roles;

        private static readonly NLog.Logger Logger = LogManager.GetLogger("logic.knowledge");

        public MetaGdl(Parser p)
        {
            _relations = new TreeDictionary<int, RelationInfo>();
            _functionSymbols = new TreeSet<int>();
            _objectSymbols = new TreeSet<int>();

            _parser = p;

            _rules = new TreeDictionary<int, List<Implication>>();
            _groundFacts = new TreeDictionary<int, List<GroundFact>>();
            _roles = new List<TermObject>();

            InsertReservedKeywords();
        }

        private void InsertReservedKeywords()
        {
            AddRelationSymbol(_parser.TokRole, 1);
            AddRelationSymbol(_parser.TokInit, 1);
            AddRelationSymbol(_parser.TokTrue, 1);
            AddRelationSymbol(_parser.TokDoes, 2);
            AddRelationSymbol(_parser.TokNext, 1);
            AddRelationSymbol(_parser.TokLegal, 2);
            AddRelationSymbol(_parser.TokGoal, 2);
            AddRelationSymbol(_parser.TokTerminal, 0);
            AddRelationSymbol(_parser.TokDistinct, 2);

            // nil isn't really a GDL token; it's only used during PLAY
            // messages to indicate that there was no previous move.
            //relationSymbols.add(parser_.TOK_NIL);


            // GDL operators
            // don't really need to add these, either.

            //public int TOK_IMPLIEDBY;
            //public int TOK_AND_OP;
            //public int TOK_OR_OP;
            //public int TOK_NOT_OP;
        }

        /**
         * Big monster master method that examines a game description and derives
         * properties from it. Should nonetheless strive to be a relatively rapid
         * process; no "game thinking" should occur here, only information about
         * straightforward "Datalog"ish facts should be examined.
         * 
         * @param gameDesc The game description to examine.
         * @return GameInformation The game information extracted from <tt>gameDesc</tt>.
         */
        public GameInformation ExamineGdl(IEnumerable<GdlExpression> gameDesc)
        {
            long startTime = DateTime.Now.Ticks;

            foreach (GdlExpression exp in gameDesc)
            {
                var list = exp as GdlList;
                if (list != null)
                    ExamineTopLevelList(list);
                else
                {
                    var atom = exp as GdlAtom;
                    if (atom != null)
                        ExamineTopLevelAtom(atom);
                }
            }

            // Now that we have our list of relations and rules, we can do some more analysis.

            // Sort static relations
            FindStaticAndInitRelations();

            // TODO: do some more meta-analysis...
            GameInformation info = MakeGameInformation();

            //TODO: this needs to be moved as it creates a circular namespace dependency
            //GameManager.AddTime(GameManager.TimeMetagdl, DateTime.Now.Ticks - startTime);

            return info;
        }

        private GameInformation MakeGameInformation()
        {
            var info = new GameInformation();

            info.SetGroundFacts(_groundFacts);
            info.SetRelations(_relations);
            info.SetRoles(_roles);
            info.SetRules(_rules);

            return info;
        }

        private void AddRelationSymbol(int symbol, int arity)
        {
            if (_relations.Contains(symbol))
                return;

            _relations[symbol] = new RelationInfo(symbol, arity);
            _groundFacts[symbol] = new List<GroundFact>();
            Logger.Debug(string.Format("MetaGDL: Adding relation symbol: {0} ({1})", symbol, _parser.SymbolTable[symbol]));
        }

        private void AddFunctionSymbol(int symbol)
        {
            if (_functionSymbols.Add(symbol))
                Logger.Debug(string.Format("MetaGDL: Adding function symbol: {0} ({1})", symbol, _parser.SymbolTable[symbol]));
        }

        private void AddObjectSymbol(int symbol)
        {
            if (_objectSymbols.Add(symbol))
                Logger.Debug(string.Format("MetaGDL: Adding object symbol: {0} ({1})", symbol, _parser.SymbolTable[symbol]));
        }

        private void AddRule(int headRelation, Implication r)
        {
            if (!_rules.Contains(headRelation))
                _rules[headRelation] = new List<Implication>();

            _rules[headRelation].Add(r);
        }

        private void AddGround(int relation, GroundFact ground)
        {
            _groundFacts[relation].Add(ground);
        }

        private void FindStaticAndInitRelations()
        {
            _staticRelations = new TreeDictionary<int, List<GroundFact>>();
            foreach (int i in _groundFacts.Keys)
            {
                if (i == _parser.TokImpliedby)
                    continue;
                List<GroundFact> relation = _groundFacts[i];
                if (i == _parser.TokInit)
                {
                    continue;
                }
                if (relation.Count > 0)
                    _staticRelations[i] = relation;
            }
        }

        private void ExamineTopLevelList(GdlList list)
        {
            // Is this a rule, or a relation?
            // Note that it is safe to assume that the head is in fact an atom.
            var head = (GdlAtom)list[0];

            if (head.GetToken() == _parser.TokImpliedby)
            {
                Implication impl = ExamineRule(list);
                AddRule(impl.Consequent.RelationName, impl);
            }
            else
            {
                // It must be a ground fact at this point: it can't have variables,
                // since there is no rule to specify binding.
                var f = (GroundFact)ExamineListRelation(list, head.GetToken());
                AddGround(head.GetToken(), f);

                // Is this a role?
                if (head.GetToken() == _parser.TokRole)
                    _roles.Add((TermObject)f.GetTerm(0));
            }
        }

        private void ExamineTopLevelAtom(GdlAtom atom)
        {
            int token = atom.GetToken();

            // This probably never happens. . . so make a note of it
            Console.WriteLine("WE GOT A TOP LEVEL ATOM!! " + _parser.SymbolTable[token]);

            //TODO: Make sure this symbol isn't a function/object symbol already.
            //if (IsFunctionSymbol(token) || IsObjectSymbol(token))
            //    throw new Exception(string.Format("Symbol '{0}' ({1}) already exists, but not as a relation symbol!", token, _parser.SymbolTable[token]));

            // It's a top level atom, so it has to be a relation symbol
            AddRelationSymbol(token, 0);
        }

        public Implication ExamineRule(GdlList rule)
        {
            // First element is the IMPLIEDBY token; ignore it.

            // Second element is the head of the rule. It's a relation (fact).
            var head = (Fact)ExamineRelation(rule[1]);

            // Everything thereafter are the antecedent relations.
            var conjuncts = new Expression[rule.Arity - 1];

            // Create the conjunct list
            for (int i = 2; i < rule.Size; i++)
                conjuncts[i - 2] = ExamineRelation(rule[i]);

            conjuncts = DistinctSorter.SortDistincts(conjuncts);

            // Create a rule structure and add it to our list.
            return new Implication(false, head, conjuncts);
        }

        public Expression ExamineRelation(GdlExpression relation)
        {
            var atom = relation as GdlAtom;
            if (atom != null)
                return ExamineAtomRelation(atom);

            // else, must be a variable
            var list = (GdlList)relation;
            return ExamineListRelation((GdlList)relation, ((GdlAtom)list[0]).GetToken());
        }

        private Expression ExamineAtomRelation(GdlAtom atom)
        {
            int relName = atom.GetToken();

            //TODO: Make sure this symbol isn't a function/object symbol already.
            //if (IsFunctionSymbol(relName) || IsObjectSymbol(relName))
            //    throw new Exception(string.Format("Symbol '{0}' ({1}) already exists, but not as a relation symbol!", relName, _parser.SymbolTable[relName]));

            // Add to relation name to our list of relation symbols
            AddRelationSymbol(relName, 0);

            return GroundFact.FromExpression(atom);
        }

        private Expression ExamineListRelation(GdlList relation, int relName)
        {
            // First: check to see if this is a logical operator, i.e. one of not/or/and

            if (relName == _parser.TokNotOp)
                // The next element must be a sentence
                return new Negation(ExamineRelation(relation[1]));

            if (relName == _parser.TokOrOp)
            {
                // all the rest of the elements are relations (sentences), not terms.
                var disjuncts = new Expression[relation.Arity];

                for (int i = 1; i <= relation.Arity; i++)
                    disjuncts[i - 1] = ExamineRelation(relation[i]);

                return new Disjunction(false, disjuncts);
            }

            // Second case: normal relation.

            //TODO: Make sure this symbol isn't a function/object symbol already.
            //if (IsFunctionSymbol(relName) || IsObjectSymbol(relName))
            //    throw new Exception(string.Format("Symbol '{0}' ({1}) already exists, but not as a relation symbol!", relName, _parser.SymbolTable[relName]));

            // Add the relation name to our list of relation symbols.
            AddRelationSymbol(relName, relation.Arity);

            // Examine each term of the relation
            for (int i = 1; i <= relation.Arity; i++)
                ExamineTerm(relation[i]);

            // Convert the relation to a (ground or variable) fact.
            return Fact.FromExpression(relation);
        }

        private void ExamineTerm(GdlExpression exp)
        {
            var atom = exp as GdlAtom;
            if (atom != null)
                ExamineAtomTerm(atom);

            else if (exp is GdlVariable)
                ExamineVariableTerm();

            else
            {
                var list = exp as GdlList;
                if (list != null)
                    ExamineListTerm(list);
                else
                    throw new Exception("MetaGdl.examineTerm: can't handle expressions of type " + exp.GetType().Name);
            }
        }

        private void ExamineAtomTerm(GdlAtom atom)
        {
            // if it's a variable, do nothing with it.
            /*if ( atom instanceof GdlVariable )
                return;*/

            int token = atom.GetToken();

            //TODO: This term must be an object constant. Make sure it is.
            //if (IsFunctionSymbol(token) || IsRelationSymbol(token))
            //    throw new Exception(string.Format("Symbol '{0}' ({1}) already exists, but not as an object symbol!", token, _parser.SymbolTable[token]));

            // Add it to our list of object symbols
            AddObjectSymbol(token);
        }

        private static void ExamineVariableTerm()
        {
            // Do nothing.
        }

        private void ExamineListTerm(GdlList list)
        {
            // The first element here must be a function symbol.
            // Note that it is safe to assume that the head is in fact an atom.
            var head = (GdlAtom)list[0];

            int token = head.GetToken();

            //TODO: Make sure that 'token' is a function symbol.
            //if (IsObjectSymbol(token) || IsRelationSymbol(token))
            //    throw new Exception("Symbol '" + token + "' (" + _parser.SymbolTable[token] + ") already exists, but not as a function symbol!");

            // Add it to our list of function symbols
            AddFunctionSymbol(token);

            // Now look at the rest of the elements in the list.
            for (int i = 1; i < list.Size; i++)
                ExamineTerm(list[i]);
        }

        ////////////////////////////////////////////////////////////////////////////

        public static GameInformation ExamineGame(String filename, Parser p)
        {
            try
            {
                GdlList axioms = p.Parse(new StreamReader(filename));
                var meta = new MetaGdl(p);
                return meta.ExamineGdl(axioms);
            }
            catch (IOException)
            {
                throw new Exception("Error reading from file");
            }
        }
    }
}
