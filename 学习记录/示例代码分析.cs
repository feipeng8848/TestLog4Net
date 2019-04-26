/*
1、动态创建RepositorySelector
*/
static LoggerManager()
		{
			try
			{
				// Register the AppDomain events, note we have to do this with a
				// method call rather than directly here because the AppDomain
				// makes a LinkDemand which throws the exception during the JIT phase.
				RegisterAppDomainEvents();
			}
			catch(System.Security.SecurityException)
			{
				LogLog.Debug(declaringType, "Security Exception (ControlAppDomain LinkDemand) while trying "+
					"to register Shutdown handler with the AppDomain. LoggerManager.Shutdown() "+
					"will not be called automatically when the AppDomain exits. It must be called "+
					"programmatically.");
			}

			// Dump out our assembly version into the log if debug is enabled
            LogLog.Debug(declaringType, GetVersionInfo());

			// Set the default repository selector
#if NETCF
			s_repositorySelector = new CompactRepositorySelector(typeof(log4net.Repository.Hierarchy.Hierarchy));
			return;
#elif !NETSTANDARD1_3
//！！！！！！！！！！！！！！！！！重点代码从这里开始
//！！！！！！！！！！！！！！！！！重点代码从这里开始
//！！！！！！！！！！！！！！！！！重点代码从这里开始
//！！！！！！！！！！！！！！！！！重点代码从这里开始
			//SystemInfo.GetAppSetting()方法是作者定义的一个方法，用于从配置文件中获得指定参数的值
			//配置文件中用户可以手动指定log4net.RepositorySelector的类型，配置文件是key value类型
			//下面的代码就是从配置文件中找到"log4net.RepositorySelector"配置段对应的value，实际上就是用户指定的RepositorySelector的类型名称
			string appRepositorySelectorTypeName = SystemInfo.GetAppSetting("log4net.RepositorySelector");
			if (appRepositorySelectorTypeName != null && appRepositorySelectorTypeName.Length > 0)
			{
				// Resolve the config string into a Type
				Type appRepositorySelectorType = null;
				try
				{
					//根据字符串加载类型，实际上执行的是Type type = Assembly.GetType(typeName, false, ignoreCase);
					appRepositorySelectorType = SystemInfo.GetTypeFromString(appRepositorySelectorTypeName, false, true);
				}
				catch(Exception ex)
				{
					LogLog.Error(declaringType, "Exception while resolving RepositorySelector Type ["+appRepositorySelectorTypeName+"]", ex);
				}

				if (appRepositorySelectorType != null)
				{
					// Create an instance of the RepositorySelectorType
					object appRepositorySelectorObj = null;
					try
					{
						//根据类型创建实例
						appRepositorySelectorObj = Activator.CreateInstance(appRepositorySelectorType);
					}
					catch(Exception ex)
					{
						LogLog.Error(declaringType, "Exception while creating RepositorySelector ["+appRepositorySelectorType.FullName+"]", ex);
					}

					//校验类型是否可以转换成IRepositorySelector
					if (appRepositorySelectorObj != null && appRepositorySelectorObj is IRepositorySelector)
					{
						s_repositorySelector = (IRepositorySelector)appRepositorySelectorObj;
					}
					else
					{
						LogLog.Error(declaringType, "RepositorySelector Type ["+appRepositorySelectorType.FullName+"] is not an IRepositorySelector");
					}
				}
			}
//！！！！！！！！！！！！！！！！！
//！！！！！！！！！！！！！！！！！
//！！！！！！！！！！！！！！！！！
//！！！！！！！！！！！！！！！！！
#endif
			// Create the DefaultRepositorySelector if not configured above 
			//如果用户没有在配置文件中指定repositorySelector的配置，会创建一个默认的repositorySelector
			if (s_repositorySelector == null)
			{
				s_repositorySelector = new DefaultRepositorySelector(typeof(log4net.Repository.Hierarchy.Hierarchy));
			}
		}
		
		//////////////////////////////////////////分割线///////////////////////////////////////////////////
		/*
		
		另外说明一点其他的东西
		
		*/
		public static ILog GetLogger(string name)
		{
            //name是用户在配置文件中配置的名字
            //System.Reflection.Assembly Assembly.GetCallingAssembly() 获取当前方法所在的程序集
            return GetLogger(Assembly.GetCallingAssembly(), name);
		}		
		//GetLogger()方法指定了当前程序集作为参数，是因为getlogger会先去当前程序集中查看是否已经创建了logger，如果没有创建才去Create一个新对象。