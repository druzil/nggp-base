using System.Collections.Generic;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model.assignments
{
    public interface AssignmentIterator : IEnumerator<TermObjectSubstitution> 
    {

        /**
	 * Request that the next assignment change at least one
	 * of the listed variables from its current assignment.
	 */
        void ChangeOneInNext(ICollection<TermVariable> varsToChange,TermObjectSubstitution assignment);

    }
}
