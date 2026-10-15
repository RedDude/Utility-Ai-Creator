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

            // Only an equal name clashes ("Ask Q" next to "Ask Quentin" is fine); a clash takes the smallest free "(n)"
            var taken = new HashSet<string>(Values
                .Where(e => e != null && !ReferenceEquals(e, element) && !string.IsNullOrEmpty(e.Name))
                .Select(e => e.Name), StringComparer.Ordinal);
            if (!taken.Contains(element.Name)) return;

            var n = 1;
            while (taken.Contains(element.Name + "(" + n + ")")) n++;
            element.Name += "(" + n + ")";
        }
        
        public void Sort()
        {
            List = List.OrderBy(o => o.Name).ToList();
            onValueChanged.OnNext(Values);
        }
    }
}