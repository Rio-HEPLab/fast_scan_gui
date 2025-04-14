import numpy as np
import matplotlib.pyplot as plt
from matplotlib.colors import LogNorm
import argparse
from scipy.optimize import curve_fit
from scipy.special import erf
from pathlib import Path


def main():

    parser = argparse.ArgumentParser(description="Returns Focal Analysis of .txt file")
    parser.add_argument('--file', type=Path, required=True, help='File path')
    parser.add_argument('--fit', action='store_true', help='Execute Curve Fitting')

    args = parser.parse_args()

    if not args.file.is_file():
        print(f"Erro: o caminho '{args.file}' não é um arquivo válido.")
        return

#abre o arquivo e salva as duas primeiras linhas como strings
    
    try:
        headerSize = 4

        #LEITURA DO ARQUIVO (a padronizar)
        with open(args.file) as file:
            for i, line in enumerate(file):
                if i==0:
                    line_parts = line.split('\t')
                    scanType = line_parts[0].split("Scan Type: ")[1]
                    axis = line_parts[1].split("Scan Axis: ")[1]
                elif i==3:
                    #salva o passo como variável float
                    step = float(line.split("Step = ")[1].strip().replace(',','.'))
                    break

        if (scanType != "SCAN_1D"):
            print("Scan Type doesn't match SCAN_1D")
            return

        #salva os dados do arquivo em um array, pulando as duas primeiras linhas
        arr = np.loadtxt(args.file, skiprows=headerSize)

        #numero de elementos
        nX = arr.shape[0]
        #PLOT DOS DADOS
        Xvec = np.arange(0, nX, 1) * step
        plt.scatter(Xvec, arr)

        if(args.fit):
            [x_fit, y_fit] = fitCurve(Xvec, arr)
            plt.plot(x_fit, y_fit)
        
        #SALVAR IMAGEM
        figOutputPath = args.file.with_suffix('.png')
        plt.savefig( figOutputPath, bbox_inches='tight' )
        print("Figure saved as " + str(figOutputPath) )
        plt.show()

    except:
        print("File format incompatible with scan")

    


def fitCurve(x, y):
    #DEFINICAO DE PARAMETROS DE MEDIA MOVEL
    y_mean_vec = []         #define vetor de media movel
    k=int(0.1*len(y))       #tamanho da janela de media movel
    #DEFINICAO DE PARAMETROS DA DERIVADA
    dy_vec = []
    dy_vec_suav = []
    t=20
    #t = int(0.1*len(y_mean_vec))    #tamanho da janela de media movel da derivada
    print(t)
    #RETIRAR OFFSET
    y = y - min(y)

    #EXECUTA FILTRO DE MEDIA MOVEL PARA SUAVIZAR CURVA
    for i in range(len(y)-k):
        ymean = sum(y[i:i+k])/k
        y_mean_vec.append(ymean)
    x_mean_vec = x[int(k/2):len(y_mean_vec)+int(k/2)]

    #ENCONTRA A DERIVADA DA CURVA
    for i in range(len(y_mean_vec)-1):
        dy = abs(y_mean_vec[i+1] - y_mean_vec[i])
        dy_vec.append(dy)
    dx_vec = x[int(k/2):len(dy_vec)+int(k/2)]
    
    #EXECUTA FILTRO DE MEDIA MOVEL PARA SUAVIZAR DERIVADA
    for i in range(len(dy_vec)-t):
        dy_suav = sum(dy_vec[i:i+t])/t
        dy_vec_suav.append(dy_suav)
    dx_vec_suav = x[int((k+t)/2):len(dy_vec_suav)+int((k+t)/2)]

    #ENCONTRA OS INDICES DE HALF RISE E HALF DROP PARA DETERMINAR O INTERVALO DE INTERESSE
    halfRise = 0.5*max(y_mean_vec)
    halfRise_index = (y_mean_vec>halfRise).argmax()
    halfDrop_index = np.argmax(y_mean_vec) + (y_mean_vec[np.argmax(y_mean_vec):] < halfRise).argmax()

    #DENTRO DO INTERVALO DE INTERESSE, DESCOBRE O PONTO DE MINIMO DA DERIVADA
    min_dy = np.argmin(dy_vec_suav[halfRise_index-int(k/2):halfDrop_index-int(k/2)])

    #DEFINICAO DOS PARAMETROS DE TRUNCAMENTO PARA AJUSTE DE CURVA
    threshold = 0.015*max(y)
    inf_lim_index = (y>threshold).argmax()
    sup_lim_index = min_dy + int((t)/2)+halfRise_index
    inf_lim = x[inf_lim_index]
    sup_lim = x[sup_lim_index]      #criterio do minimo da derivada
    
    x_data = x[(x>inf_lim) & (x<sup_lim)]
    y_data = y[(x>inf_lim) & (x<sup_lim)]

    #PLOT DO INTERVALO DE INTERESSE
    #plt.plot(dx_vec_suav, dy_vec_suav)
    #plt.plot(x[int(t/2)+halfRise_index:int(t/2)+halfDrop_index],dy_vec_suav[halfRise_index-int(k/2):halfDrop_index-int(k/2)])
    #print(len(dx_vec_suav), len(dy_vec_suav))

    #AJUSTE DE CURVA
    #guess para os valores iniciais dos parâmentros
    p0 = [0.05, x[halfRise_index+int(k/2)], (y[sup_lim_index]-y[inf_lim_index]), y[inf_lim_index]]
    popt, pcov = curve_fit(rise_function, x_data, y_data, p0)

    print(popt)
    sig, x0, I, C= popt

    yopt = rise_function(x_data, sig, x0, I, C)
    print("Sigma: ", sig)

    return [x_data, yopt+min(y)]


#define a função para o ajuste de curva
def rise_function(x, sigma, x0, I, C):
    return 0.5*I*(1+erf((x-x0)/(np.sqrt(2)*sigma))) + C

if __name__ == "__main__":
    main()