﻿namespace Entity.ValueType
{
    using Platform.Enum;
    using Platform.Extension;
    using System.Collections.Generic;
    using System.Linq;

    public class ProjectTask
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string UserId { get; set; }

        public double PlanHour { get; set; }

        public double ActualHour { get; set; }

        public Phase Phase { get; set; }

        public DateRange PlanDateRange { get; set; }
        public DateRange ActualDateRange { get; set; }

        public Status Status { get; set; }

        public string CodeReview { get; set; }
        public Dictionary<string, int> Values { get; set;}

        public bool IsReviewed { get; set; }

        public int CalculateValue()
        {
            if (Values.IsEmpty())
            {
                return 0;
            }
            else
            {
                var vaildValues = Values.Where(o => o.Value > 0).ToList();

                if(vaildValues.Count == 0)
                {
                    return 0;
                }

                var total = vaildValues.Sum(o => o.Value);
                var min = vaildValues.Min(o => o.Value);
                var max = vaildValues.Max(o => o.Value);
                var count = vaildValues.Count;

                if(count < 2)
                {
                    return total;
                }
                else if(count == 2)
                {
                    return min;
                }
                else
                {
                    return (total - min - max) / (count - 2);
                }
            }
        }
    }
}
