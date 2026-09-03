namespace Bookstore.Domain.ReferenceData
{
    public class ReferenceDataItem : Entity
    {
        // An empty constructor is required by EF Core
#pragma warning disable CS8618 // EF Core requires a parameterless constructor; Text is always populated by EF.
        private ReferenceDataItem() { }
#pragma warning restore CS8618

        public ReferenceDataItem(ReferenceDataType referenceDataType, string text)
        {
            DataType = referenceDataType;
            Text = text;
        }

        public ReferenceDataType DataType { get; set; }

        public string Text { get; set; }
    }
}
