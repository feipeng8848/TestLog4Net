一、log4net核心原理
1、ILogger（日志记录者）
在Log4Net架构中，对日志的记录是以日志实体为单位的。它记录日志的最低级别（高于此基本的消息都可以记录）
ILogger拥有自己的Appender集合，负责输出日志。
		Appender拥有自己的
				过滤器Filter和
				布局Layout。
	
2、ILoggerRespository（Logger仓库）
ILoggerRespository接口中维护着LevelMap、PluginMap、Rendermap。
	LevelMap、PluginMap分别是Level,Plugin对象以其名称为键的集合；
	Rendermap是IObjectRender以其类型为键的集合。
ILoggerRespository中维护并存储着这些对象的集合（LevelMap、PluginMap、Rendermap），但它并不负责创建这些对象，而是把创建的工作委托给了XmlHierarchyConfigurator。
	XmlHierarchyConfigurator负责解析对Log4net的Xml（节点）配置。
	从ILogRepository中可以获取到Logger，但是该接口并没有定义存储Logger的变量（包括LevelMap、PluginMap、Rendermap这些，也都没有定义实际用于存储的变量）
	实际上Logger是在实现了ILoggerResposity的实体类中存储的。

3、LoggerRepositorySkeleton
LoggerRepositorySkeleton是一个抽象类，继承自ILoggerRepositry，实现了某些方法。创建了用于存储LevelMap、PluginMap、Rendermap的变量。

4、Hierarchy
Hierarchy继承了LoggerRepositorySkeleton, IBasicRepositoryConfigurator, IXmlRepositoryConfigurator
	（a）LoggerRepositorySkeleton又继承自ILoggerRepository, Appender.IFlushable，所以是一个Repository
	（b）IBasicRepositoryConfigurator, IXmlRepositoryConfigurator是两个读取xml配置的接口
	（c）Hierarchy存储着log4net所有的logger，这些logger是从创建Logger的工厂Hierarchy.LoggerFactory中创建的.这些logger被创建出来后存储在Hierarchy.m_ht中，m_ht是一个hashtable
	
二、获取Logger的流程
用户使用log4net的时候，调用LogManager.GetLogger()方法即可得到一个logger（ILog的实例对象）然后开始记录。
1、ILog
ILog提供了用户使用的接口，供用户使用，记录日志，主要是便于用户理解和使用，它对外隐藏了Log4net内复杂的架构和实现原理。
对用户而言，通过LogManager.GetLogger()获取ILog对象，然后用ILog对象的Info()、Debug()、Error()等方法记录日志。

2、ILogger
在框架中“日志对象”是ILogger的实现类，ILog对象只是对ILogger对象的包装，以便于用户的理解和使用。
“日志对象”（ILogger的实例）通常是log4net通过XML来配置创建的。

3、ILog的实例对象（实际上封装的是ILogger）的获取过程
ILog对象是通过LogManager工具类来获取的。
LogManager通过LoggerManager获取ILogger对象（注意LogManager和LoggerManager不一样），然后使用ILoggerWraper对其进行包装，并通过WrapperMap进行管理Logger和ILoggerWraper之间的映射。
LogManager是从 ILoggerRepository中获取ILogger对象。
欲获取ILogger对象，必须首先获取ILoggerRepository对象。
IRepositorySelector就是负责缓存和管理ILoggerRepository对象的类，所以需要通过IRepositorySelector获取ILogger对象所在的ILoggerRepository对象，然后再从ILoggerRepository对象中获取ILogger对象。
实际上，ILoggerRepository对象也是从别处获取的ILogger对象，真正存储ILogger对象的是Hierarchy（Logger仓库）。

流程：LogManager -> LoggerManager -> IRepositorySelector -> ILoggerRepository -> Hierarchy

以上就是LogManager.GetLogger()方法的具体逻辑。

三、日志的写入流程
Logger -> Appender -> Filter -> Layout -> Render -> LoggingEvent

四、log4net整个工作流程
找调度(LogManager)要个干活的工人（Logger，写日志的对象），当然，方法是调用LogManager.GetLogger()。
找个什么样工人，究竟是那个工人会被挑中详见第三节《获取Logger的流程》。
先不管这么细节，知道有个能干活的工人(Logger) 肯定是被找来了。
这工人有经纪人ILoggerWrapper（Logger需要实现ILogger, 而ILoggerWrapper唯一的方法就是得到ILogger实例），
经纪人又有代理ILog(ILog继承于ILoggerWrapper)。
代理ILog存在的意义在于给你提供方便的接口函数（Info,Warn等等）
而不是工人提供的void Log(string callerFullName, Level level, object message, Exception t)。 
不管关系多复杂，虽然你让干什么活都得先对代理说，但最后还都是告诉了工人，一个字也没落。

通过Info(“hello”)告诉工人干活了，工人Logger一定先看看这事能不能干。
你的配置里说只写Info这个级别以上的信息，咱就不能写Debug和Warn。这种情况你需要付出性能代价（一个函数调用和一个整数形式的级别比较）。
然后，工人Logger就创建一个任务包LoggingEvent，把你要做的事儿用任务包的形式包起来，以后的流程就都针对任务包LoggingEvent处理了。
任务包LoggingEvent里信息丰富，包含：时间代码位置、工人的名字、信息、线程名、用户名、信息、异常、上下文等等。 

接下来，Appender们登场了。
原来工人自己不干具体的活，手里拽着一堆马仔，自己成了工头，告诉Appender去DoAppend()，让马仔们干活。
注意，这里说得是“马仔们”，就是说同时会有多个马仔都在写东东。究竟那些马仔能被选中完成这光荣的任务，还要由客户您来决定。

说到这儿，检查员Filter登场。这活最终究竟干不干，马仔还得通过Decide()再问问检查员们。注意，这里说得是“检查员们”，就是说所有在册的检查员都点头，这话才能干。如何让检查员在册，看配置文件。

检查员们点头后，这事就必须要干了。怎么干？客户要写的东东究竟用什么格式输出？这活由排版员Layout来干。
排版员需要排版LoggingEvent的信息的字符串内容RenderedMessage，例如文章开头的“hello world!”。
除了“hello world!”这样的字符串，信息message还可以是任意的对象。因此需要针对对象进行专门的排版，由Render（对象打印机）来干。

一切就绪，各个马仔就做最后的输出，有打印屏幕的，有写文件的，有在网络上发数据的，八仙过海，各显神通。 

——————————————————————————————————————————————
王正坤，各个接口意义与层级详解
——————————————————————————————————————————————
1、log4net.Repository.ILoggerRepository

方法：
ILogger Exists(string name);								//检查名为name的Logger是否存在，如果存在就返回这Logger，如果不存在就返回null
ILogger[] GetCurrentLoggers();								//获取当前已经定义的所有的Logger
log4net.Appender.IAppender[] GetAppenders();				//获取当前已经定义的所有Appender
ILogger GetLogger(string name);								//获取名为name的Logger
void Log(LoggingEvent logEvent);
void ResetConfiguration();									//重置当前Repository的配置为默认值
void Shutdown();											//关闭当前Repository

属性：
ICollection ConfigurationMessages { get; set; }				//在最近的配置过程中捕获的内部消息的集合。
bool Configured { get; set; }								//一个Flag，表明当前Repository有没有被配置
LevelMap LevelMap { get; }									//LevelMap类是一个集合，Hashtable类型
PluginMap PluginMap { get; }								//PluginMap类是一个集合，Hashtable类型
RendererMap RendererMap { get; }							//RendererMap类是一个集合，Hashtable类型
PropertiesDictionary Properties { get; }					//Util中定义的一个类
string Name { get; set; }									//Repository的名称
Level Threshold { get; set; }								//此Repository中所有事件的阈值

事件：
event LoggerRepositoryConfigurationChangedEventHandler ConfigurationChanged;			//配置改变	
event LoggerRepositoryConfigurationResetEventHandler ConfigurationReset;				//配置重置
event LoggerRepositoryShutdownEventHandler ShutdownEvent;								//关闭Repository

2、log4net.Repository.IXmlRepositoryConfigurator
方法：
void Configure(System.Xml.XmlElement element);				//配置Repository
															//Hierarchy中实现了该方法，实际上是用XmlHierarchyConfigurator类的对象做了实现
															//但XmlHierarchyConfigurator类并没有继承IXmlRepositoryConfigurator接口





















