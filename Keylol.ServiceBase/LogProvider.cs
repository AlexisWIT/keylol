using log4net;

namespace Keylol.ServiceBase
{
    /// <summary>
    /// log4net �ṩ��
    /// </summary>
    public interface ILogProvider
    {
        /// <summary>
        /// ��ȡ Logger
        /// </summary>
        ILog Logger { get; }
    }

    /// <summary>
    /// log4net �ṩ��
    /// </summary>
    /// <typeparam name="T">�����ض����͵� Logger</typeparam>
    public class LogProvider<T> : ILogProvider
    {
        /// <summary>
        /// �� LogManager.GetLogger ������ȡ�� Logger
        /// </summary>
        public ILog Logger { get; } = LogManager.GetLogger(typeof (T));
    }
}