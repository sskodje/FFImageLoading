﻿using System;
using FFImageLoading.Work;
using FFImageLoading.Config;

namespace FFImageLoading.DataResolvers
{
    public class DataResolverFactory : IDataResolverFactory
    {
        static DataResolverFactory instance;
        internal static DataResolverFactory Instance
        {
            get
            {
                if (instance == null)
                    instance = new DataResolverFactory();
                return instance;
            }
        }

        public virtual IDataResolver GetResolver(string identifier, ImageSource source, TaskParameter parameters, Configuration configuration)
        {
            switch (source)
            {
                case ImageSource.ApplicationBundle:
                case ImageSource.Filepath:
                    return new FileDataResolver();
                case ImageSource.CompiledResource:
                    return new BundleDataResolver();
                case ImageSource.Url:
                    if (!string.IsNullOrWhiteSpace(identifier) && identifier.IsDataUrl())
                        return new DataUrlResolver();                    
                    return new UrlDataResolver(configuration);
                case ImageSource.Stream:
                    return new StreamDataResolver();
                case ImageSource.EmbeddedResource:
                    return new EmbeddedResourceResolver();
                default:
                    throw new NotSupportedException("Unknown type of ImageSource");
            }
        }
    }
}

