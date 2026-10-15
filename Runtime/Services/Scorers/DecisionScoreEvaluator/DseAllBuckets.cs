using System.Collections.Generic;

public class DseAllBuckets: DecisionScoreEvaluator
{
    private const string Name = Consts.Name_AllBucketsDSE;
    private const string Description = Consts.Description_AllBucketsDSE;
    public override string GetDescription()
    {
        return Description;
    }

    public override string GetName()
    {
        return Name;
    }
    
    public override List<AgentAction> NextActions(List<Bucket> buckets, Uai uai)
    {
        var context = uai.UaiContext;
        foreach (var bucket in buckets)
        {
            context.CurrentEvaluatedBucket = bucket;
            bucket.GetUtility(context);
        }

        // Buckets are compared in the Uai's list order, and every decision of every bucket is scored: a decision may score above
        // its bucket's weight (a Base Weight above 1), so no bucket is skipped. Utilities are compared rounded to 3 decimals, so
        // an equal score at 3 decimals goes to the first in (bucket list, decision list) order instead of to float noise.
        Decision bestDecision = null;
        Bucket bestBucket = null;
        var bestUtility = float.MinValue;
        foreach (var bucket in buckets)
        {
            context.CurrentEvaluatedBucket = bucket;
            foreach (var decision in bucket.Decisions.Values)
            {
                context.CurrentEvaluatedDecision = decision;
                var utility = Quantize(decision.GetUtility(context));
                if (utility > bestUtility)
                {
                    bestUtility = utility;
                    bestDecision = decision;
                    bestBucket = bucket;
                }
            }
        }

        if (bestDecision == null) return new List<AgentAction>();

        SetContextValuesBucket(context, bestBucket);
        SetContextValuesDecision(context, bestDecision);
        return bestDecision.AgentActions.Values;
    }

    private static float Quantize(float utility) =>
        (float)System.Math.Round((double)utility, 3, System.MidpointRounding.AwayFromZero);

    public override List<AgentAction> NextActions(List<Decision> decisions, Uai uai)
    {
        throw new System.NotImplementedException();
    }
}