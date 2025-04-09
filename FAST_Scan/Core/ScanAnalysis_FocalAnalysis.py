import numpy as np
import matplotlib.pyplot as plt
from matplotlib.colors import LogNorm
import argparse
from pathlib import Path


def main():

    parser = argparse.ArgumentParser(description="Returns Focal Analysis of .txt file")
    parser.add_argument('--file', type=Path, required=True, help='File path')

    args = parser.parse_args()

    if not args.file.is_file():
        print(f"Erro: o caminho '{args.file}' não é um arquivo válido.")
        return

#abre o arquivo e salva as duas primeiras linhas como strings
    
    try:
        #LEITURA DO ARQUIVO (a padronizar)
        with open(args.file) as file:
            line1 = file.readline().strip()
            line2 = file.readline().strip()

        #salva os passos em X e Y como variáveis float
        stepX = float(line1.split("Step X = ")[1].strip().replace(',','.'))
        stepY = float(line2.split("Step Y = ")[1].strip().replace(',','.'))

        #salva os dados do arquivo em um array, pulando as duas primeiras linhas
        arr = np.loadtxt(args.file, skiprows=2)

        #numero de elementos em X e Y
        nX = arr.shape[1]
        nY = arr.shape[0]

        #ANALISE DOS DADOS
        
        
        #SALVAR IMAGEM
        figOutputPath = args.file.with_suffix('.png')
        plt.savefig( figOutputPath, bbox_inches='tight' )
        print("Figure saved as " + str(figOutputPath) )
        plt.show()
    except:
        print("File format incompatible with scan")

if __name__ == "__main__":
    main()