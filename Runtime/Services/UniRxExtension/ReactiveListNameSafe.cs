using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniRxExtension
{
    public class ReactiveListNameSafe<T> : ReactiveList<T> where T : AiObjectModel
    {

        public override void Add(T element)
        {
            MakeNameUnique(element);
            base.Add(element);
        }

        public override void Add(IEnumerable<T> elements)
        {
            var aiObjectModels = elements.ToList();
            foreach (var element in aiObjectModels)
            {
                MakeNameUnique(element);
            }
            base.Add(aiObjectModels);
        }

        // New elements (e.g. freshly created actions) can have a null name, and so can existing ones.
        private void MakeNameUnique(T element)
        {
            if (string.IsNullOrEmpty(element.Name)) return;

            var numberOfIdenticalNames = Values
                .Count(e => e != null && !string.IsNullOrEmpty(e.Name) && e.Name.Contains(element.Name));

            if (numberOfIdenticalNames > 0)
            {
                element.Name += "(" + numberOfIdenticalNames + ")";
            }
        }
        
        public void Sort()
        {
            List = List.OrderBy(o => o.Name).ToList();
            onValueChanged.OnNext(Values);
        }
    }
}