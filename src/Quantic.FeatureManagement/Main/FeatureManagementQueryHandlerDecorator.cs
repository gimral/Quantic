﻿using System.Linq;
using System.Threading.Tasks;
using Quantic.Core;

namespace Quantic.FeatureManagement
{
    public class FeatureManagementQueryHandlerDecorator<TQuery, TResponse> : IQueryHandler<TQuery, TResponse>
               where TQuery : IQuery<TResponse>
    {
        private readonly SettingsHolder featureSettingsHolder;
        private readonly IHandlerFeatureInfoProvider handlerFeatureInfoProvider;
        private readonly IQueryHandler<TQuery,TResponse> decoratedRequestHandler;

        public FeatureManagementQueryHandlerDecorator(IQueryHandler<TQuery,TResponse> decoratedRequestHandler,
            SettingsHolder featureSettingsHolder,
            IHandlerFeatureInfoProvider handlerFeatureInfoProvider)
        {
            this.decoratedRequestHandler = decoratedRequestHandler;
            this.featureSettingsHolder = featureSettingsHolder;
            this.handlerFeatureInfoProvider = handlerFeatureInfoProvider;
        }

        public async Task<QueryResult<TResponse>> Handle(TQuery query, RequestContext context)
        {
            var handlerInfo = handlerFeatureInfoProvider.GetHandlerInfo(query.GetType().Name);

            if (handlerInfo == null)
                return await decoratedRequestHandler.Handle(query, context);

            var allFeaturesEnabled = handlerInfo.Features.All(feature => 
            {
                return featureSettingsHolder.Settings.FirstOrDefault(x=>x.FeatureName == feature).Enabled(context);
            });

            if (!allFeaturesEnabled)
                return QueryResult<TResponse>.WithCode(default(TResponse), Msg.FeatureNotEnabled);

            var usedFeatureHeaders = handlerInfo.Features.Select(featureName => $"{Header.Prefix}-{featureName}");

            foreach (var header in usedFeatureHeaders)
            {
                context.Add(header, handlerInfo.Name);
            }
            
            return await decoratedRequestHandler.Handle(query, context);            
        }            
    }
}
