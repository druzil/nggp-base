using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using nJocLogic.data;

namespace nJocLogic.reasoner.AimaProver
{

public class Substitution
{
	private readonly Dictionary<TermVariable, Term> _contents;

	public Substitution()
	{
		_contents = new Dictionary<TermVariable, Term>();
	}

	public Substitution Compose(Substitution thetaPrime)
	{
		var result = new Substitution();

	    foreach (var kv in _contents)
	        result._contents[kv.Key] = kv.Value;

        foreach (var kv in thetaPrime._contents)
            result._contents[kv.Key] = kv.Value;

		return result;
	}

	public bool Contains(TermVariable variable)
	{
		return _contents.ContainsKey(variable);
	}

	public override bool Equals(Object o)
	{
        var substitution = o as Substitution;
	    return substitution != null && substitution._contents.Equals(_contents);
	}

    public Term this[TermVariable variable]
    {
        get
        {
            return _contents[variable];    
        }
        set
        {
            _contents[variable] = value;
        }
    }

    public override int GetHashCode()
    {
        //return _contents.GetHashCode();
        //INFO: java hashmap hashcode
        return _contents.Sum(e => (e.Key == null ? 0 : e.Key.GetHashCode()) ^ (e.Value == null ? 0 : e.Value.GetHashCode()));
    }

    /**
	 * Creates an identical substitution.
	 *
	 * @return A new, identical substitution.
	 */
	public Substitution Copy()
	{
		var copy = new Substitution();
        foreach (var kv in _contents)
            copy._contents[kv.Key] = kv.Value;
		return copy;
	}

    public override string ToString()
    {
		StringBuilder sb = new StringBuilder();

		sb.Append("{ ");
        foreach (TermVariable variable in _contents.Keys)
            sb.Append(variable + "/" + _contents[variable] + " ");
        sb.Append("}");

		return sb.ToString();
	}

}

}
