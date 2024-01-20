using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using HandsLiftedApp.Extensions;
using System.Collections.Generic;

namespace HandsLiftedApp.Utils
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
