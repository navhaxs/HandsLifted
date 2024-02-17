using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Metadata;
using HandsLiftedApp.Extensions;

namespace HandsLiftedApp.Common
{
    public class MyTemplateSelector : IDataTemplate
    {

        public bool SupportsRecycling => false;
        [Content]
        public Dictionary<string, IDataTemplate> Templates { get; } = new Dictionary<string, IDataTemplate>();

        public Control Build(object data)
        {
            if (data == null)
                return null;

            try
            {
                var dataType = data.GetType().GetNameWithoutGenericArity();

                if (Templates.ContainsKey(dataType))
                {
                    return Templates[dataType].Build(data);
                }

                // TODO possible to loop by x:DataType ???
                // experiment:
                // var keyValuePair = Templates.First().Value;
                // if (keyValuePair is DataTemplate dt)
                // {
                //     dt.DataType.GetNameWithoutGenericArity() == dataType;
                // }
                // if (Templates.First(template => template.Value.))

                else if (Templates.ContainsKey("Fallback"))
                {
                    return Templates["Fallback"].Build(data);
                }
            }
            catch (System.Exception)
            {
                return null;
            }


            return null;
        }

        public bool Match(object data)
        {
            //return data is SlideState;
            return true;
        }
    }
}
