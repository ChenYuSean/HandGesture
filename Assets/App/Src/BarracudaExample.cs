using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;


namespace HandTask.Barracuda
{
    public class BarracudaExample : MonoBehaviour
    {
        public NNModel modelAsset;
        public Model model;
        public IWorker modelWorker;

        public void Start()
        {
            Debug.Log(this.modelAsset);

            this.model = ModelLoader.Load(this.modelAsset);


            Debug.Log(this.model);

            this.modelWorker = WorkerFactory.CreateWorker(
                this.model, WorkerFactory.Device.GPU);

            Debug.Log(this.modelWorker);
        }

        public void Update()
        {
            if (Input.GetKeyDown("space"))
                Run();
        }

        public void Run()
        {
            int batch = 1;
            int height = 1;
            int width = 1;
            int channel = 43;

            Tensor input = new Tensor(batch, height, width, channel);
            this.modelWorker.Execute(input);

            Tensor output = this.modelWorker.PeekOutput();

            output.PrintDataPart(output.length);
            Debug.Log(output.ArgMax()[0]);

            input.Dispose();
        }

        public void OnDestroy()
        {
            this.modelWorker.Dispose();
        }
    }
}
